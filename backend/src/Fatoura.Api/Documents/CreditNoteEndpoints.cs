using System.ComponentModel.DataAnnotations;
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

public sealed record CreditNoteLineRequest(int InvoiceLineId, decimal Quantity);

public sealed record CreditNoteRequest(
    int InvoiceId,
    DateOnly? Date,
    [property: Required, MaxLength(500)] string Reason,
    bool ReturnToStock,
    List<CreditNoteLineRequest> Lines);

public sealed record CreditNoteLineDto(
    int Id,
    int LineNo,
    int InvoiceLineId,
    int? ItemId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    Fatoura.Domain.Tax.TaxCategory TaxCategory,
    decimal VatRate,
    decimal Discount,
    decimal Net,
    decimal Vat,
    decimal Total);

public sealed record CreditNoteDto(
    int Id,
    string Number,
    DateOnly Date,
    int InvoiceId,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    string Reason,
    bool ReturnToStock,
    PartyDto Client,
    CompanyDto Company,
    DocumentCurrencyDto Currency,
    decimal Discount,
    decimal SubTotal,
    decimal VatTotal,
    decimal Total,
    Guid CreatedById,
    string CreatedByName,
    DateTimeOffset CreatedAt,
    List<CreditNoteLineDto> Lines);

public sealed record CreditNoteSummaryListDto(
    int Id, string Number, DateOnly Date, int InvoiceId, string InvoiceNumber, string ClientName, string Currency, decimal Total, string CreatedByName);

public static class CreditNoteEndpoints
{
    public static RouteGroupBuilder MapCreditNoteEndpoints(this RouteGroupBuilder api)
    {
        // BRD §2: cashiers cannot delete invoices but can issue a credit note (return) against one.
        var group = api.MapGroup("/credit-notes").WithTags("Credit Notes").RequireAuthorization(Policies.Staff);
        group.MapGet("/", List);
        group.MapGet("/{id:int}", Get);
        group.MapPost("/", Create);
        return group;
    }

    public static async Task<CreditNoteDto> LoadDtoAsync(FatouraDbContext db, int id, CancellationToken ct)
    {
        var c = await db.CreditNotes.AsNoTracking().AsSplitQuery().Include(x => x.Lines).Include(x => x.Invoice).Include(x => x.CreatedBy)
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Credit note");
        return new CreditNoteDto(
            c.Id, c.Number, c.Date, c.InvoiceId, c.Invoice!.Number, c.Invoice.Date, c.Reason, c.ReturnToStock,
            c.ClientSnapshot.ToDto(), c.CompanySnapshot.ToDto(), c.CurrencyDto(), c.Discount, c.SubTotal, c.VatTotal, c.Total, c.CreatedById,
            c.CreatedBy?.DisplayName ?? string.Empty, c.CreatedAt,
            c.Lines.OrderBy(l => l.LineNo).Select(l => new CreditNoteLineDto(
                l.Id, l.LineNo, l.InvoiceLineId, l.ItemId, l.Description, l.Quantity, l.UnitPrice, l.TaxCategory, l.VatRate, l.Discount, l.Net, l.Vat, l.Total)).ToList());
    }

    private static async Task<Ok<PagedResult<CreditNoteSummaryListDto>>> List(
        FatouraDbContext db, string? search, int? invoiceId, DateOnly? from, DateOnly? to, int? page, int? pageSize, CancellationToken ct)
    {
        var q = db.CreditNotes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = Paging.Like(search);
            q = q.Where(c => EF.Functions.Like(c.Number, like) || EF.Functions.Like(c.Invoice!.Number, like) || EF.Functions.Like(c.ClientSnapshot.Name, like));
        }

        if (invoiceId is { } i)
        {
            q = q.Where(c => c.InvoiceId == i);
        }

        if (from is { } f)
        {
            q = q.Where(c => c.Date >= f);
        }

        if (to is { } t)
        {
            q = q.Where(c => c.Date <= t);
        }

        var result = await q.OrderByDescending(c => c.Date).ThenByDescending(c => c.Id)
            .Select(c => new CreditNoteSummaryListDto(c.Id, c.Number, c.Date, c.InvoiceId, c.Invoice!.Number, c.ClientSnapshot.Name, c.Currency, c.Total, c.CreatedBy!.DisplayName))
            .ToPagedAsync(page, pageSize, ct);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<CreditNoteDto>> Get(int id, FatouraDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await LoadDtoAsync(db, id, ct));

    private static async Task<Created<CreditNoteDto>> Create(
        CreditNoteRequest r,
        FatouraDbContext db,
        SequenceService sequences,
        InvoiceService invoices,
        StockService stock,
        SettingsService settingsService,
        ICurrentUser user,
        BusinessClock clock,
        TimeProvider time,
        CancellationToken ct)
    {
        var v = new FieldValidator()
            .Require(!string.IsNullOrWhiteSpace(r.Reason), "reason", "A reason is required.")
            .Require(r.Lines is { Count: > 0 }, "lines", "Select at least one line to credit.");
        v.ThrowIfInvalid();
        var date = InvoiceEndpoints.ResolveDate(r.Date, user, clock);

        var id = await db.InTransactionAsync(async () =>
        {
            var settings = await settingsService.RequireCompleteAsync(ct);
            var number = await sequences.NextNumberAsync(DocumentType.CreditNote, date, ct);
            var invoice = await invoices.LockAsync(r.InvoiceId, ct);
            if (invoice.Status == InvoiceStatus.Void)
            {
                throw new ConflictException("A void invoice cannot be credited.");
            }

            if (date < invoice.Date)
            {
                throw InvalidRequestException.For("date", "A credit note cannot be dated before its invoice.");
            }

            var lineIds = invoice.Lines.Select(l => l.Id).ToList();
            var alreadyCredited = await db.CreditNoteLines.Where(l => lineIds.Contains(l.InvoiceLineId))
                .GroupBy(l => l.InvoiceLineId).Select(g => new { g.Key, Qty = g.Sum(l => l.Quantity), Discount = g.Sum(l => l.Discount) })
                .ToDictionaryAsync(x => x.Key, x => (x.Qty, x.Discount), ct);

            var requests = new List<DocumentLineRequest>();
            var invoiceLines = new List<InvoiceLine>();
            var requestedPerLine = new Dictionary<int, decimal>();
            var lv = new FieldValidator();
            foreach (var (req, i) in r.Lines.Select((x, i) => (x, i)))
            {
                var line = invoice.Lines.SingleOrDefault(l => l.Id == req.InvoiceLineId);
                if (line is null)
                {
                    lv.Add($"lines[{i}].invoiceLineId", "This line does not belong to the invoice.");
                    continue;
                }

                var remaining = line.Quantity - alreadyCredited.GetValueOrDefault(line.Id).Qty - requestedPerLine.GetValueOrDefault(line.Id);
                lv.Require(req.Quantity > 0, $"lines[{i}].quantity", "Quantity must be greater than zero.")
                  .Require(Domain.Documents.DocumentCalculator.HasAtMostDecimals(req.Quantity, 3), $"lines[{i}].quantity", "Quantity can have at most 3 decimals.")
                  .Require(req.Quantity <= remaining, $"lines[{i}].quantity", $"Only {remaining:0.###} can still be credited on this line.");
                requestedPerLine[line.Id] = requestedPerLine.GetValueOrDefault(line.Id) + req.Quantity;
                requests.Add(new DocumentLineRequest(line.ItemId, line.Description, req.Quantity, line.UnitPrice, line.TaxCategory));
                invoiceLines.Add(line);
            }

            lv.ThrowIfInvalid();

            var now = time.GetUtcNow();
            var note = new CreditNote
            {
                Number = number,
                Date = date,
                InvoiceId = invoice.Id,
                Reason = r.Reason.Trim(),
                ReturnToStock = r.ReturnToStock,
                ClientSnapshot = invoice.ClientSnapshot.Clone(),
                CompanySnapshot = invoice.CompanySnapshot.Clone(),
                CreatedById = user.RequireId(),
                CreatedAt = now,
            };

            // Credit lines reuse the invoice line's price, tax category and VAT rate (not today's settings), and take back
            // the matching share of its discount: proportional to the quantity, with the last credit taking what is left
            // so a fully credited line returns exactly its discount.
            var creditedSoFar = alreadyCredited.ToDictionary(x => x.Key, x => x.Value);
            for (var i = 0; i < requests.Count; i++)
            {
                var src = invoiceLines[i];
                var input = new Domain.Documents.LineInput(requests[i].Quantity, src.UnitPrice, src.TaxCategory);
                var (qtyBefore, discountBefore) = creditedSoFar.GetValueOrDefault(src.Id);
                var discount = qtyBefore + requests[i].Quantity == src.Quantity
                    ? src.Discount - discountBefore
                    : Domain.Documents.DocumentCalculator.RoundMoney(src.Discount * requests[i].Quantity / src.Quantity);
                discount = Math.Clamp(discount, 0, Domain.Documents.DocumentCalculator.Gross(input));
                creditedSoFar[src.Id] = (qtyBefore + requests[i].Quantity, discountBefore + discount);
                var amounts = Domain.Documents.DocumentCalculator.CalculateLine(input, src.VatRate, discount);
                note.Lines.Add(new CreditNoteLine
                {
                    LineNo = i + 1,
                    InvoiceLineId = src.Id,
                    ItemId = src.ItemId,
                    Description = src.Description,
                    Quantity = requests[i].Quantity,
                    UnitPrice = src.UnitPrice,
                    TaxCategory = src.TaxCategory,
                    VatRate = src.VatRate,
                    Discount = amounts.Discount,
                    Net = amounts.Net,
                    Vat = amounts.Vat,
                    Total = amounts.Total,
                });
            }

            note.Discount = note.Lines.Sum(l => l.Discount);
            note.SubTotal = note.Lines.Sum(l => l.Net);
            note.VatTotal = note.Lines.Sum(l => l.Vat);
            note.Total = note.SubTotal + note.VatTotal;

            // A credit note adjusts the original supply, so it uses the invoice's currency and rate.
            note.ApplyCurrency(new DocumentCurrency(invoice.Currency, invoice.ExchangeRate), note.Lines);
            db.CreditNotes.Add(note);
            await db.SaveChangesAsync(ct);

            if (r.ReturnToStock)
            {
                var changes = note.Lines.Where(l => l.ItemId is not null).GroupBy(l => l.ItemId!.Value)
                    .Select(g => new StockChange(g.Key, g.Sum(l => l.Quantity), StockMovementType.CreditNote, note.Number, CreditNoteId: note.Id));
                await stock.ApplyAsync(changes, allowNegative: true, ct);
                await db.SaveChangesAsync(ct);
            }

            return note.Id;
        }, ct);

        return TypedResults.Created($"/api/credit-notes/{id}", await LoadDtoAsync(db, id, ct));
    }
}
