using Fatoura.Api.Auth;
using Fatoura.Api.Data;
using Fatoura.Api.Data.Entities;
using Fatoura.Api.Infrastructure;
using Fatoura.Api.Inventory;
using Fatoura.Api.Settings;
using Fatoura.Domain.Numbering;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Fatoura.Api.Documents;

public sealed record QuotationDto(
    int Id,
    string Number,
    DateOnly Date,
    DateOnly ValidUntil,
    bool IsExpired,
    int ClientId,
    PartyDto Client,
    QuotationStatus Status,
    int? ConvertedInvoiceId,
    string? ConvertedInvoiceNumber,
    TermsDto Terms,
    DocumentDiscountDto Discount,
    decimal SubTotal,
    decimal VatTotal,
    decimal Total,
    Guid CreatedById,
    string CreatedByName,
    DateTimeOffset CreatedAt,
    List<DocumentLineDto> Lines);

public sealed record QuotationSummaryDto(
    int Id,
    string Number,
    DateOnly Date,
    DateOnly ValidUntil,
    bool IsExpired,
    int ClientId,
    string ClientName,
    QuotationStatus Status,
    decimal Total,
    int? ConvertedInvoiceId,
    string CreatedByName);

public sealed record QuotationRequest(
    int ClientId,
    DateOnly? Date,
    DateOnly? ValidUntil,
    List<DocumentLineRequest> Lines,
    TermsRequest? Terms,
    DocumentDiscountRequest? Discount = null);

public sealed record QuotationStatusRequest(QuotationStatus Status);

public sealed record ConvertQuotationRequest(PaymentRequest? Payment);

public static class QuotationEndpoints
{
    public static RouteGroupBuilder MapQuotationEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/quotations").WithTags("Quotations").RequireAuthorization(Policies.Staff);
        group.MapGet("/", List);
        group.MapGet("/{id:int}", Get);
        group.MapPost("/", Create);
        group.MapPut("/{id:int}", Update);
        group.MapPut("/{id:int}/status", SetStatus);
        group.MapPost("/{id:int}/convert", Convert);
        group.MapDelete("/{id:int}", Delete).RequireAuthorization(Policies.Admin);
        return group;
    }

    public static async Task<QuotationDto> LoadDtoAsync(FatouraDbContext db, BusinessClock clock, int id, CancellationToken ct)
    {
        var q = await db.Quotations.AsNoTracking().AsSplitQuery().Include(x => x.Lines).Include(x => x.CreatedBy)
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Quotation");
        var invoiceNumber = q.ConvertedInvoiceId is { } invoiceId
            ? await db.Invoices.Where(i => i.Id == invoiceId).Select(i => i.Number).SingleOrDefaultAsync(ct)
            : null;
        return new QuotationDto(
            q.Id, q.Number, q.Date, q.ValidUntil, IsExpired(q, clock.Today), q.ClientId, q.ClientSnapshot.ToDto(), q.Status,
            q.ConvertedInvoiceId, invoiceNumber, q.Terms(), q.DiscountDto(), q.SubTotal, q.VatTotal, q.Total, q.CreatedById,
            q.CreatedBy?.DisplayName ?? string.Empty, q.CreatedAt, q.Lines.OrderBy(l => l.LineNo).Select(l => l.ToDto()).ToList());
    }

    private static bool IsExpired(Quotation q, DateOnly today) =>
        q.Status is not (QuotationStatus.Converted or QuotationStatus.Rejected) && q.ValidUntil < today;

    private static async Task<Ok<PagedResult<QuotationSummaryDto>>> List(
        FatouraDbContext db,
        BusinessClock clock,
        string? search,
        int? clientId,
        QuotationStatus? status,
        DateOnly? from,
        DateOnly? to,
        bool? openOnly,
        int? page,
        int? pageSize,
        CancellationToken ct)
    {
        var today = clock.Today;
        var q = db.Quotations.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = Paging.Like(search);
            q = q.Where(x => EF.Functions.Like(x.Number, like) || EF.Functions.Like(x.ClientSnapshot.Name, like));
        }

        if (clientId is { } c)
        {
            q = q.Where(x => x.ClientId == c);
        }

        if (status is { } s)
        {
            q = q.Where(x => x.Status == s);
        }

        if (from is { } f)
        {
            q = q.Where(x => x.Date >= f);
        }

        if (to is { } t)
        {
            q = q.Where(x => x.Date <= t);
        }

        if (openOnly == true)
        {
            q = q.Where(x => (x.Status == QuotationStatus.Draft || x.Status == QuotationStatus.Sent || x.Status == QuotationStatus.Accepted)
                && x.ValidUntil >= today);
        }

        var result = await q.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
            .Select(x => new QuotationSummaryDto(
                x.Id, x.Number, x.Date, x.ValidUntil,
                x.Status != QuotationStatus.Converted && x.Status != QuotationStatus.Rejected && x.ValidUntil < today,
                x.ClientId, x.ClientSnapshot.Name, x.Status, x.Total, x.ConvertedInvoiceId, x.CreatedBy!.DisplayName))
            .ToPagedAsync(page, pageSize, ct);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<QuotationDto>> Get(int id, FatouraDbContext db, BusinessClock clock, CancellationToken ct) =>
        TypedResults.Ok(await LoadDtoAsync(db, clock, id, ct));

    private static async Task<Created<QuotationDto>> Create(
        QuotationRequest r,
        FatouraDbContext db,
        SequenceService sequences,
        SettingsService settingsService,
        ICurrentUser user,
        BusinessClock clock,
        TimeProvider time,
        CancellationToken ct)
    {
        var v = new FieldValidator();
        await DocumentLines.ValidateAsync(r.Lines, db, v, ct);
        DocumentLines.ValidateDiscount(r.Discount, r.Lines, v);
        v.ThrowIfInvalid();

        var id = await db.InTransactionAsync(async () =>
        {
            var settings = await settingsService.RequireCompleteAsync(ct);
            var client = await db.Clients.SingleOrDefaultAsync(c => c.Id == r.ClientId, ct)
                ?? throw InvalidRequestException.For("clientId", "Client not found.");
            var date = r.Date ?? clock.Today;
            var now = time.GetUtcNow();
            var q = new Quotation
            {
                Number = await sequences.NextNumberAsync(DocumentType.Quotation, date, ct),
                Date = date,
                ValidUntil = r.ValidUntil ?? date.AddDays(settings.QuotationValidityDays),
                ClientId = client.Id,
                ClientSnapshot = PartySnapshot.From(client),
                Status = QuotationStatus.Draft,
                CreatedById = user.RequireId(),
                CreatedAt = now,
                UpdatedAt = now,
            };
            ValidateDates(q.Date, q.ValidUntil);
            q.ApplyTerms(r.Terms, settings);
            q.Build(r.Lines, r.Discount, settings.VatRate, q.Lines);
            db.Quotations.Add(q);
            await db.SaveChangesAsync(ct);
            return q.Id;
        }, ct);

        return TypedResults.Created($"/api/quotations/{id}", await LoadDtoAsync(db, clock, id, ct));
    }

    private static async Task<Ok<QuotationDto>> Update(
        int id,
        QuotationRequest r,
        FatouraDbContext db,
        SettingsService settingsService,
        ICurrentUser user,
        BusinessClock clock,
        TimeProvider time,
        CancellationToken ct)
    {
        var v = new FieldValidator();
        await DocumentLines.ValidateAsync(r.Lines, db, v, ct, allowInactiveItems: true);
        DocumentLines.ValidateDiscount(r.Discount, r.Lines, v);
        v.ThrowIfInvalid();

        await db.InTransactionAsync(async () =>
        {
            var q = await LoadEditableAsync(db, id, user, ct);
            var settings = await settingsService.GetAsync(ct);
            var client = await db.Clients.SingleOrDefaultAsync(c => c.Id == r.ClientId, ct)
                ?? throw InvalidRequestException.For("clientId", "Client not found.");

            q.ClientId = client.Id;
            q.ClientSnapshot = PartySnapshot.From(client);
            q.Date = r.Date ?? q.Date;
            q.ValidUntil = r.ValidUntil ?? q.ValidUntil;
            ValidateDates(q.Date, q.ValidUntil);
            q.ApplyTerms(r.Terms, settings);
            db.QuotationLines.RemoveRange(q.Lines);
            var lines = new List<QuotationLine>();
            q.Build(r.Lines, r.Discount, settings.VatRate, lines);
            q.Lines = lines;
            q.UpdatedAt = time.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);

        return TypedResults.Ok(await LoadDtoAsync(db, clock, id, ct));
    }

    private static async Task<Ok<QuotationDto>> SetStatus(
        int id, QuotationStatusRequest r, FatouraDbContext db, ICurrentUser user, BusinessClock clock, TimeProvider time, CancellationToken ct)
    {
        if (r.Status == QuotationStatus.Converted)
        {
            throw InvalidRequestException.For("status", "Use Convert to Invoice to convert a quotation.");
        }

        var q = await LoadEditableAsync(db, id, user, ct);
        q.Status = r.Status;
        q.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await LoadDtoAsync(db, clock, id, ct));
    }

    /// <summary>One-click conversion (BRD §3.5): issues a tax invoice from the quotation's client, lines and terms.</summary>
    private static async Task<Created<InvoiceResult>> Convert(
        int id,
        ConvertQuotationRequest? r,
        FatouraDbContext db,
        InvoiceService invoices,
        BusinessClock clock,
        TimeProvider time,
        CancellationToken ct)
    {
        var (invoiceId, warnings) = await db.InTransactionAsync(async () =>
        {
            await db.Database.ExecuteSqlAsync($"SELECT [Id] FROM [Quotations] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {id}", ct);
            var q = await db.Quotations.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Quotation");
            if (q.Status == QuotationStatus.Converted)
            {
                throw new ConflictException($"This quotation was already converted to an invoice.", "already_converted");
            }

            if (q.Status == QuotationStatus.Rejected)
            {
                throw new ConflictException("A rejected quotation cannot be converted.");
            }

            var lines = q.Lines.OrderBy(l => l.LineNo).Select(l => l.ToRequest()).ToList();
            var v = new FieldValidator();
            await DocumentLines.ValidateAsync(lines, db, v, ct);
            v.ThrowIfInvalid();

            var terms = new TermsRequest(q.PaymentTerms, q.CompletionOfWork, q.Notes, q.ClosingText);
            var (invoice, w) = await invoices.IssueAsync(
                new IssueInvoiceCommand(q.ClientId, clock.Today, lines, terms, q.DiscountRequest(), q.Id, q.Number, r?.Payment), ct);
            q.Status = QuotationStatus.Converted;
            q.ConvertedInvoiceId = invoice.Id;
            q.UpdatedAt = time.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return (invoice.Id, w);
        }, ct);

        return TypedResults.Created($"/api/invoices/{invoiceId}",
            new InvoiceResult(await InvoiceEndpoints.LoadDtoAsync(db, invoiceId, ct), warnings.ToList()));
    }

    private static async Task<NoContent> Delete(int id, FatouraDbContext db, CancellationToken ct)
    {
        var q = await db.Quotations.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Quotation");
        if (q.Status == QuotationStatus.Converted)
        {
            throw new ConflictException("A converted quotation cannot be deleted.");
        }

        db.Quotations.Remove(q);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>Cashiers may change only their own quotations; nobody may change a converted one.</summary>
    private static async Task<Quotation> LoadEditableAsync(FatouraDbContext db, int id, ICurrentUser user, CancellationToken ct)
    {
        var q = await db.Quotations.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Quotation");
        if (!user.IsAdmin && q.CreatedById != user.Id)
        {
            throw new ForbiddenException("Only the creator or an Admin can change this quotation.");
        }

        if (q.Status == QuotationStatus.Converted)
        {
            throw new ConflictException("A converted quotation can no longer be changed.");
        }

        return q;
    }

    private static void ValidateDates(DateOnly date, DateOnly validUntil)
    {
        if (validUntil < date)
        {
            throw InvalidRequestException.For("validUntil", "Valid-until must be on or after the quotation date.");
        }
    }
}
