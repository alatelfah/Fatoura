using System.ComponentModel.DataAnnotations;
using Fatoura.Api.Auth;
using Fatoura.Api.Data;
using Fatoura.Api.Data.Entities;
using Fatoura.Api.Infrastructure;
using Fatoura.Api.Inventory;
using Fatoura.Api.Settings;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Fatoura.Api.Documents;

public sealed record PaymentRequest(decimal Amount, PaymentMethod Method, DateOnly? Date, [property: MaxLength(100)] string? Reference);

public sealed record PaymentDto(int Id, DateOnly Date, decimal Amount, PaymentMethod Method, string Reference, DateTimeOffset CreatedAt);

public sealed record CreditNoteSummaryDto(int Id, string Number, DateOnly Date, decimal Total, string Reason);

public sealed record InvoiceLineDto(
    int Id,
    int LineNo,
    int? ItemId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    Fatoura.Domain.Tax.TaxCategory TaxCategory,
    decimal VatRate,
    decimal Discount,
    decimal Net,
    decimal Vat,
    decimal Total,
    decimal CreditedQuantity);

public sealed record InvoiceDto(
    int Id,
    string Number,
    DateOnly Date,
    int ClientId,
    PartyDto Client,
    CompanyDto Company,
    int? QuotationId,
    string QuotationNumber,
    InvoiceStatus Status,
    string VoidReason,
    DateTimeOffset? VoidedAt,
    TermsDto Terms,
    DocumentDiscountDto Discount,
    DocumentCurrencyDto Currency,
    decimal SubTotal,
    decimal VatTotal,
    decimal Total,
    decimal PaidTotal,
    decimal CreditedTotal,
    decimal Balance,
    Guid CreatedById,
    string CreatedByName,
    DateTimeOffset CreatedAt,
    List<InvoiceLineDto> Lines,
    List<PaymentDto> Payments,
    List<CreditNoteSummaryDto> CreditNotes);

public sealed record InvoiceSummaryDto(
    int Id,
    string Number,
    DateOnly Date,
    int ClientId,
    string ClientName,
    InvoiceStatus Status,
    string Currency,
    decimal Total,
    decimal PaidTotal,
    decimal CreditedTotal,
    decimal Balance,
    string CreatedByName);

public sealed record IssueInvoiceRequest(
    int ClientId,
    DateOnly? Date,
    List<DocumentLineRequest> Lines,
    TermsRequest? Terms,
    PaymentRequest? Payment,
    DocumentDiscountRequest? Discount = null,
    [property: MaxLength(3)] string? Currency = null,
    decimal? ExchangeRate = null);

/// <summary>Omitting the currency or rate keeps the invoice's own.</summary>
public sealed record UpdateInvoiceRequest(
    int ClientId,
    DateOnly Date,
    List<DocumentLineRequest> Lines,
    TermsRequest? Terms,
    DocumentDiscountRequest? Discount = null,
    [property: MaxLength(3)] string? Currency = null,
    decimal? ExchangeRate = null);

public sealed record VoidInvoiceRequest([property: Required, MaxLength(500)] string Reason);

public sealed record InvoiceResult(InvoiceDto Invoice, List<StockWarning> Warnings);

public static class InvoiceEndpoints
{
    public static RouteGroupBuilder MapInvoiceEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/invoices").WithTags("Invoices");
        group.MapGet("/", List).RequireAuthorization(Policies.Staff);
        group.MapGet("/{id:int}", Get).RequireAuthorization(Policies.Staff);
        group.MapPost("/", Issue).RequireAuthorization(Policies.Staff);
        group.MapPut("/{id:int}", Update).RequireAuthorization(Policies.Admin);
        group.MapPost("/{id:int}/void", Void).RequireAuthorization(Policies.Admin);
        group.MapPost("/{id:int}/payments", AddPayment).RequireAuthorization(Policies.Staff);
        group.MapDelete("/{id:int}/payments/{paymentId:int}", DeletePayment).RequireAuthorization(Policies.Admin);
        return group;
    }

    /// <summary>Cashiers always issue with today's date; Admins may back-date but never post-date.</summary>
    public static DateOnly ResolveDate(DateOnly? requested, ICurrentUser user, BusinessClock clock)
    {
        var today = clock.Today;
        if (requested is null || !user.IsAdmin)
        {
            return today;
        }

        if (requested > today)
        {
            throw InvalidRequestException.For("date", "The invoice date cannot be in the future.");
        }

        return requested.Value;
    }

    public static async Task<InvoiceDto> LoadDtoAsync(FatouraDbContext db, int id, CancellationToken ct)
    {
        var i = await db.Invoices.AsNoTracking().AsSplitQuery()
            .Include(x => x.Lines).Include(x => x.Payments).Include(x => x.CreditNotes).Include(x => x.CreatedBy)
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Invoice");

        var lineIds = i.Lines.Select(l => l.Id).ToList();
        var credited = await db.CreditNoteLines.AsNoTracking().Where(l => lineIds.Contains(l.InvoiceLineId))
            .GroupBy(l => l.InvoiceLineId).Select(g => new { g.Key, Qty = g.Sum(l => l.Quantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

        var paid = i.Payments.Sum(p => p.Amount);
        var creditedTotal = i.CreditNotes.Sum(c => c.Total);
        return new InvoiceDto(
            i.Id, i.Number, i.Date, i.ClientId, i.ClientSnapshot.ToDto(), i.CompanySnapshot.ToDto(), i.QuotationId, i.QuotationNumber,
            i.Status, i.VoidReason, i.VoidedAt, i.Terms(), i.DiscountDto(), i.CurrencyDto(), i.SubTotal, i.VatTotal, i.Total, paid, creditedTotal,
            i.Status == InvoiceStatus.Void ? 0 : i.Total - creditedTotal - paid,
            i.CreatedById, i.CreatedBy?.DisplayName ?? string.Empty, i.CreatedAt,
            i.Lines.OrderBy(l => l.LineNo).Select(l => new InvoiceLineDto(
                l.Id, l.LineNo, l.ItemId, l.Description, l.Quantity, l.UnitPrice, l.TaxCategory, l.VatRate, l.Discount, l.Net, l.Vat, l.Total,
                credited.GetValueOrDefault(l.Id))).ToList(),
            i.Payments.OrderBy(p => p.Date).ThenBy(p => p.Id).Select(p => new PaymentDto(p.Id, p.Date, p.Amount, p.Method, p.Reference, p.CreatedAt)).ToList(),
            i.CreditNotes.OrderBy(c => c.Id).Select(c => new CreditNoteSummaryDto(c.Id, c.Number, c.Date, c.Total, c.Reason)).ToList());
    }

    private static async Task<Ok<PagedResult<InvoiceSummaryDto>>> List(
        FatouraDbContext db,
        string? search,
        int? clientId,
        Guid? createdById,
        InvoiceStatus? status,
        DateOnly? from,
        DateOnly? to,
        bool? unpaidOnly,
        int? page,
        int? pageSize,
        CancellationToken ct)
    {
        var q = db.Invoices.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = Paging.Like(search);
            q = q.Where(i => EF.Functions.Like(i.Number, like) || EF.Functions.Like(i.ClientSnapshot.Name, like));
        }

        if (clientId is { } c)
        {
            q = q.Where(i => i.ClientId == c);
        }

        if (createdById is { } u)
        {
            q = q.Where(i => i.CreatedById == u);
        }

        if (status is { } s)
        {
            q = q.Where(i => i.Status == s);
        }

        if (from is { } f)
        {
            q = q.Where(i => i.Date >= f);
        }

        if (to is { } t)
        {
            q = q.Where(i => i.Date <= t);
        }

        var projected = q.Select(i => new
        {
            i.Id,
            i.Number,
            i.Date,
            i.ClientId,
            ClientName = i.ClientSnapshot.Name,
            i.Status,
            i.Currency,
            i.Total,
            Paid = i.Payments.Sum(p => (decimal?)p.Amount) ?? 0,
            Credited = i.CreditNotes.Sum(cn => (decimal?)cn.Total) ?? 0,
            CreatedByName = i.CreatedBy!.DisplayName,
        });

        if (unpaidOnly == true)
        {
            projected = projected.Where(i => i.Status == InvoiceStatus.Issued && i.Total - i.Credited - i.Paid > 0);
        }

        var result = await projected.OrderByDescending(i => i.Date).ThenByDescending(i => i.Id)
            .Select(i => new InvoiceSummaryDto(i.Id, i.Number, i.Date, i.ClientId, i.ClientName, i.Status, i.Currency, i.Total, i.Paid, i.Credited,
                i.Status == InvoiceStatus.Void ? 0 : i.Total - i.Credited - i.Paid, i.CreatedByName))
            .ToPagedAsync(page, pageSize, ct);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<InvoiceDto>> Get(int id, FatouraDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await LoadDtoAsync(db, id, ct));

    private static async Task<Created<InvoiceResult>> Issue(
        IssueInvoiceRequest r, FatouraDbContext db, InvoiceService invoices, ICurrentUser user, BusinessClock clock, CancellationToken ct)
    {
        var v = new FieldValidator();
        await DocumentLines.ValidateAsync(r.Lines, db, v, ct);
        DocumentLines.ValidateDiscount(r.Discount, r.Lines, v);
        var currency = await CurrencyEndpoints.ResolveAsync(db, r.Currency, r.ExchangeRate, v, ct);
        v.ThrowIfInvalid();
        var date = ResolveDate(r.Date, user, clock);

        var (id, warnings) = await db.InTransactionAsync(async () =>
        {
            var (invoice, w) = await invoices.IssueAsync(new IssueInvoiceCommand(r.ClientId, date, r.Lines, r.Terms, r.Discount, currency, Payment: r.Payment), ct);
            return (invoice.Id, w);
        }, ct);

        return TypedResults.Created($"/api/invoices/{id}", new InvoiceResult(await LoadDtoAsync(db, id, ct), warnings.ToList()));
    }

    private static async Task<Ok<InvoiceResult>> Update(
        int id, UpdateInvoiceRequest r, FatouraDbContext db, InvoiceService invoices, ICurrentUser user, BusinessClock clock, CancellationToken ct)
    {
        var v = new FieldValidator();
        await DocumentLines.ValidateAsync(r.Lines, db, v, ct, allowInactiveItems: true);
        DocumentLines.ValidateDiscount(r.Discount, r.Lines, v);
        var existing = await db.Invoices.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException("Invoice");
        var currency = await CurrencyEndpoints.ResolveAsync(db, r.Currency, r.ExchangeRate, v, ct, existing);
        v.ThrowIfInvalid();
        var date = ResolveDate(r.Date, user, clock);

        var warnings = await db.InTransactionAsync(async () =>
        {
            var (_, w) = await invoices.EditAsync(id, r.ClientId, date, r.Lines, r.Discount, currency, r.Terms, ct);
            return w;
        }, ct);

        return TypedResults.Ok(new InvoiceResult(await LoadDtoAsync(db, id, ct), warnings.ToList()));
    }

    private static async Task<Ok<InvoiceDto>> Void(int id, VoidInvoiceRequest r, FatouraDbContext db, InvoiceService invoices, CancellationToken ct)
    {
        new FieldValidator().Require(!string.IsNullOrWhiteSpace(r.Reason), "reason", "A reason is required.").ThrowIfInvalid();
        await db.InTransactionAsync(async () => await invoices.VoidAsync(id, r.Reason, ct), ct);
        return TypedResults.Ok(await LoadDtoAsync(db, id, ct));
    }

    private static async Task<Ok<InvoiceDto>> AddPayment(
        int id, PaymentRequest r, FatouraDbContext db, InvoiceService invoices, TimeProvider time, BusinessClock clock, CancellationToken ct)
    {
        if (r.Date is { } d && d > clock.Today)
        {
            throw InvalidRequestException.For("date", "Payment date cannot be in the future.");
        }

        await db.InTransactionAsync(async () =>
        {
            var invoice = await invoices.LockAsync(id, ct);
            if (invoice.Status == InvoiceStatus.Void)
            {
                throw new ConflictException("Payments cannot be recorded on a void invoice.");
            }

            var credited = await db.CreditNotes.Where(c => c.InvoiceId == id).SumAsync(c => (decimal?)c.Total, ct) ?? 0;
            InvoiceService.ValidatePayment(r, invoice.Total - credited - invoice.Payments.Sum(p => p.Amount), prefix: string.Empty);
            invoice.Payments.Add(invoices.NewPayment(r, time.GetUtcNow()));
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);

        return TypedResults.Ok(await LoadDtoAsync(db, id, ct));
    }

    private static async Task<Ok<InvoiceDto>> DeletePayment(int id, int paymentId, FatouraDbContext db, CancellationToken ct)
    {
        var payment = await db.Payments.SingleOrDefaultAsync(p => p.Id == paymentId && p.InvoiceId == id, ct)
            ?? throw new NotFoundException("Payment");
        db.Payments.Remove(payment);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await LoadDtoAsync(db, id, ct));
    }
}
