using Fatoura.Api.Data;
using Fatoura.Api.Data.Entities;
using Fatoura.Api.Infrastructure;
using Fatoura.Api.Inventory;
using Fatoura.Api.Settings;
using Fatoura.Domain.Numbering;
using Microsoft.EntityFrameworkCore;

namespace Fatoura.Api.Documents;

public sealed record IssueInvoiceCommand(
    int ClientId,
    DateOnly Date,
    IReadOnlyList<DocumentLineRequest> Lines,
    TermsRequest? Terms,
    DocumentDiscountRequest? Discount = null,
    int? QuotationId = null,
    string QuotationNumber = "",
    PaymentRequest? Payment = null);

/// <summary>Issues, edits and voids tax invoices. Every method must run inside <see cref="DbTransactions.InTransactionAsync{T}"/>.</summary>
public sealed class InvoiceService(
    FatouraDbContext db,
    SequenceService sequences,
    StockService stock,
    SettingsService settingsService,
    ICurrentUser currentUser,
    BusinessClock clock,
    TimeProvider time)
{
    public async Task<(Invoice Invoice, IReadOnlyList<StockWarning> Warnings)> IssueAsync(IssueInvoiceCommand cmd, CancellationToken ct)
    {
        var settings = await settingsService.RequireCompleteAsync(ct);
        var client = await db.Clients.SingleOrDefaultAsync(c => c.Id == cmd.ClientId, ct)
            ?? throw InvalidRequestException.For("clientId", "Client not found.");

        var number = await sequences.NextNumberAsync(DocumentType.Invoice, cmd.Date, ct);
        var now = time.GetUtcNow();
        var invoice = new Invoice
        {
            Number = number,
            Date = cmd.Date,
            ClientId = client.Id,
            ClientSnapshot = PartySnapshot.From(client),
            CompanySnapshot = CompanySnapshot.From(settings),
            QuotationId = cmd.QuotationId,
            QuotationNumber = cmd.QuotationNumber,
            Status = InvoiceStatus.Issued,
            CreatedById = currentUser.RequireId(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        invoice.ApplyTerms(cmd.Terms, settings);
        invoice.Build(cmd.Lines, cmd.Discount, settings.VatRate, invoice.Lines);
        await SetUnitCostsAsync(invoice.Lines, ct);

        if (cmd.Payment is { } p)
        {
            ValidatePayment(p, invoice.Total);
            invoice.Payments.Add(NewPayment(p, now));
        }

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);

        var warnings = await stock.ApplyAsync(
            SaleChanges(invoice, invoice.Lines, -1, StockMovementType.Sale), settings.AllowNegativeStock, ct);
        await db.SaveChangesAsync(ct);
        return (invoice, warnings);
    }

    /// <summary>Admin edit: replaces client, date, lines and terms; the number never changes. Stock is re-applied.</summary>
    public async Task<(Invoice Invoice, IReadOnlyList<StockWarning> Warnings)> EditAsync(
        int id,
        int clientId,
        DateOnly date,
        IReadOnlyList<DocumentLineRequest> lines,
        DocumentDiscountRequest? discount,
        TermsRequest? terms,
        CancellationToken ct)
    {
        var invoice = await LockAsync(id, ct);
        if (invoice.Status == InvoiceStatus.Void)
        {
            throw new ConflictException("A void invoice cannot be edited.");
        }

        if (await db.CreditNotes.AnyAsync(c => c.InvoiceId == id, ct))
        {
            throw new ConflictException("This invoice has credit notes and can no longer be edited.", "has_credit_notes");
        }

        var settings = await settingsService.GetAsync(ct);
        var client = await db.Clients.SingleOrDefaultAsync(c => c.Id == clientId, ct)
            ?? throw InvalidRequestException.For("clientId", "Client not found.");

        var oldLines = invoice.Lines.ToList();
        var newLines = new List<InvoiceLine>();
        invoice.Build(lines, discount, settings.VatRate, newLines);
        await SetUnitCostsAsync(newLines, ct);

        var paid = invoice.Payments.Sum(p => p.Amount);
        if (paid > invoice.Total)
        {
            throw new ConflictException($"Payments ({paid:0.00}) exceed the new total ({invoice.Total:0.00}). Remove a payment first.");
        }

        db.InvoiceLines.RemoveRange(oldLines);
        invoice.Lines = newLines;
        invoice.ClientId = client.Id;
        invoice.ClientSnapshot = PartySnapshot.From(client);
        invoice.Date = date;
        invoice.ApplyTerms(terms, settings);
        invoice.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);

        var changes = SaleChanges(invoice, oldLines, +1, StockMovementType.SaleReversal)
            .Concat(SaleChanges(invoice, newLines, -1, StockMovementType.Sale));
        var warnings = await stock.ApplyAsync(Net(changes), settings.AllowNegativeStock, ct);
        await db.SaveChangesAsync(ct);
        return (invoice, warnings);
    }

    /// <summary>Admin "delete": the invoice keeps its number, is excluded from totals and returns its stock.</summary>
    public async Task<Invoice> VoidAsync(int id, string reason, CancellationToken ct)
    {
        var invoice = await LockAsync(id, ct);
        if (invoice.Status == InvoiceStatus.Void)
        {
            throw new ConflictException("This invoice is already void.");
        }

        if (await db.CreditNotes.AnyAsync(c => c.InvoiceId == id, ct))
        {
            throw new ConflictException("This invoice has credit notes; it cannot be voided.", "has_credit_notes");
        }

        invoice.Status = InvoiceStatus.Void;
        invoice.VoidReason = reason.Trim();
        invoice.VoidedAt = time.GetUtcNow();
        invoice.VoidedById = currentUser.RequireId();
        invoice.UpdatedAt = invoice.VoidedAt.Value;
        await stock.ApplyAsync(SaleChanges(invoice, invoice.Lines, +1, StockMovementType.Void), allowNegative: true, ct);
        await db.SaveChangesAsync(ct);
        return invoice;
    }

    public static void ValidatePayment(PaymentRequest p, decimal balance, string prefix = "payment.")
    {
        new FieldValidator()
            .Require(p.Amount > 0, prefix + "amount", "Amount must be greater than zero.")
            .Require(Domain.Documents.DocumentCalculator.HasAtMostDecimals(p.Amount, 2), prefix + "amount", "Amount can have at most 2 decimals.")
            .Require(p.Amount <= balance, prefix + "amount", $"Amount cannot exceed the balance due ({balance:0.00}).")
            .Require(Enum.IsDefined(p.Method), prefix + "method", "Unknown payment method.")
            .ThrowIfInvalid();
    }

    public Payment NewPayment(PaymentRequest p, DateTimeOffset now) => new()
    {
        Date = p.Date ?? clock.Today,
        Amount = p.Amount,
        Method = p.Method,
        Reference = p.Reference?.Trim() ?? string.Empty,
        CreatedById = currentUser.RequireId(),
        CreatedAt = now,
    };

    /// <summary>Loads the invoice with an update lock so concurrent edits, voids and credit notes serialise.</summary>
    public async Task<Invoice> LockAsync(int id, CancellationToken ct)
    {
        await db.Database.ExecuteSqlAsync($"SELECT [Id] FROM [Invoices] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {id}", ct);
        return await db.Invoices.Include(i => i.Lines).Include(i => i.Payments).SingleOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("Invoice");
    }

    private async Task SetUnitCostsAsync(IEnumerable<InvoiceLine> lines, CancellationToken ct)
    {
        var list = lines.ToList();
        var costs = await stock.CostsAsync(list.Where(l => l.ItemId is not null).Select(l => l.ItemId!.Value), ct);
        foreach (var line in list)
        {
            line.UnitCost = line.ItemId is { } itemId && costs.TryGetValue(itemId, out var cost) ? cost : 0m;
        }
    }

    private static IEnumerable<StockChange> SaleChanges(Invoice invoice, IEnumerable<InvoiceLine> lines, int sign, StockMovementType type) =>
        lines.Where(l => l.ItemId is not null)
            .GroupBy(l => l.ItemId!.Value)
            .Select(g => new StockChange(g.Key, sign * g.Sum(l => l.Quantity), type, invoice.Number, InvoiceId: invoice.Id));

    /// <summary>Combines reversal and re-application per item so an unchanged line produces no movement.</summary>
    private static IEnumerable<StockChange> Net(IEnumerable<StockChange> changes) =>
        changes.GroupBy(c => c.ItemId)
            .Select(g =>
            {
                var qty = g.Sum(c => c.Quantity);
                var type = qty < 0 ? StockMovementType.Sale : StockMovementType.SaleReversal;
                var first = g.First();
                return first with { Quantity = qty, Type = type, Note = "Invoice edited" };
            })
            .Where(c => c.Quantity != 0);
}
