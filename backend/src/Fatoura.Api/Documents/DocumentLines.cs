using System.ComponentModel.DataAnnotations;
using Fatoura.Api.Data;
using Fatoura.Api.Data.Entities;
using Fatoura.Api.Infrastructure;
using Fatoura.Domain.Documents;
using Fatoura.Domain.Tax;
using Microsoft.EntityFrameworkCore;

namespace Fatoura.Api.Documents;

/// <summary>Validated by <see cref="DocumentLines.ValidateAsync"/> so all line errors are reported together.</summary>
public sealed record DocumentLineRequest(
    int? ItemId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    TaxCategory TaxCategory);

public sealed record DocumentLineDto(
    int Id,
    int LineNo,
    int? ItemId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    TaxCategory TaxCategory,
    decimal VatRate,
    decimal Discount,
    decimal Net,
    decimal Vat,
    decimal Total);

/// <summary>A document-level discount: <c>Kind</c> Amount (AED) or Percent (of the sub total); omit or use None for no discount.</summary>
public sealed record DocumentDiscountRequest(DiscountKind Kind, decimal Value);

/// <summary>The discount as entered, and <c>Amount</c>, its value in money. The sub total before discount is <c>SubTotal + Amount</c>.</summary>
public sealed record DocumentDiscountDto(DiscountKind Kind, decimal Value, decimal Amount);

public sealed record PartyDto(string Name, string Address, string Phone, string Email, string Trn);

public sealed record CompanyDto(string Name, string Address, string Phone, string Email, string Website, string Trn);

public sealed record TermsDto(string PaymentTerms, string CompletionOfWork, string Notes, string ClosingText);

/// <summary>Optional per-document terms; omitted values fall back to the defaults in Settings.</summary>
public sealed record TermsRequest(
    [property: MaxLength(1000)] string? PaymentTerms,
    [property: MaxLength(1000)] string? CompletionOfWork,
    [property: MaxLength(4000)] string? Notes,
    [property: MaxLength(1000)] string? ClosingText);

public sealed record DocumentTotalsDto(decimal SubTotal, decimal VatTotal, decimal Total)
{
    public decimal Discount { get; init; }
}

public static class DocumentLines
{
    public const int MaxLines = 200;

    public static PartyDto ToDto(this PartySnapshot p) => new(p.Name, p.Address, p.Phone, p.Email, p.Trn);

    public static CompanyDto ToDto(this CompanySnapshot c) => new(c.Name, c.Address, c.Phone, c.Email, c.Website, c.Trn);

    public static TermsDto Terms(this IDocumentTerms t) => new(t.PaymentTerms, t.CompletionOfWork, t.Notes, t.ClosingText);

    public static DocumentLineDto ToDto(this DocumentLineBase l) =>
        new(l.Id, l.LineNo, l.ItemId, l.Description, l.Quantity, l.UnitPrice, l.TaxCategory, l.VatRate, l.Discount, l.Net, l.Vat, l.Total);

    public static DocumentDiscountDto DiscountDto(this IDiscounted d) => new(d.DiscountKind, d.DiscountValue, d.Discount);

    public static DocumentDiscount ToDiscount(this DocumentDiscountRequest? r) =>
        r is null ? DocumentDiscount.None : new DocumentDiscount(r.Kind, r.Value);

    public static DocumentDiscountRequest? DiscountRequest(this IDiscounted d) =>
        d.DiscountKind == DiscountKind.None ? null : new DocumentDiscountRequest(d.DiscountKind, d.DiscountValue);

    /// <summary>
    /// Validates a document discount against already-valid <paramref name="lines"/>: a percentage of at most 100, or an
    /// amount no larger than the sub total, each with at most 2 decimals.
    /// </summary>
    public static void ValidateDiscount(DocumentDiscountRequest? discount, IReadOnlyList<DocumentLineRequest> lines, FieldValidator v)
    {
        if (discount is null || discount.Kind == DiscountKind.None || !v.IsValid)
        {
            return;
        }

        if (!Enum.IsDefined(discount.Kind))
        {
            v.Add("discount.kind", "Unknown discount kind.");
            return;
        }

        var gross = lines.Sum(l => DocumentCalculator.Gross(new LineInput(l.Quantity, l.UnitPrice, l.TaxCategory)));
        v.Require(discount.Value >= 0, "discount.value", "A discount cannot be negative.")
         .Require(DocumentCalculator.HasAtMostDecimals(discount.Value, DocumentCalculator.MoneyDecimals), "discount.value", "A discount can have at most 2 decimals.")
         .Require(discount.Kind != DiscountKind.Percent || discount.Value <= 100, "discount.value", "A discount percentage cannot exceed 100.")
         .Require(discount.Kind != DiscountKind.Amount || discount.Value <= gross, "discount.value", $"A discount cannot exceed the sub total ({gross:0.00}).");
    }

    public static void ApplyTerms(this IDocumentTerms target, TermsRequest? request, CompanySettings defaults)
    {
        target.PaymentTerms = request?.PaymentTerms?.Trim() ?? defaults.PaymentTerms;
        target.CompletionOfWork = request?.CompletionOfWork?.Trim() ?? defaults.CompletionOfWork;
        target.Notes = request?.Notes?.Trim() ?? defaults.Notes;
        target.ClosingText = request?.ClosingText?.Trim() ?? defaults.ClosingText;
    }

    /// <summary>Validates line requests (shape, decimals, referenced items) and adds any problems to <paramref name="v"/>.</summary>
    public static async Task ValidateAsync(
        IReadOnlyList<DocumentLineRequest>? lines, FatouraDbContext db, FieldValidator v, CancellationToken ct, bool allowInactiveItems = false)
    {
        if (lines is null || lines.Count == 0)
        {
            v.Add("lines", "Add at least one line.");
            return;
        }

        if (lines.Count > MaxLines)
        {
            v.Add("lines", $"A document can have at most {MaxLines} lines.");
        }

        var itemIds = lines.Where(l => l.ItemId is not null).Select(l => l.ItemId!.Value).Distinct().ToList();
        var items = await db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.IsActive }).ToDictionaryAsync(i => i.Id, i => i.IsActive, ct);

        for (var i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            var p = $"lines[{i}]";
            v.Require(!string.IsNullOrWhiteSpace(l.Description), $"{p}.description", "Description is required.");
            v.Require((l.Description?.Length ?? 0) <= 1000, $"{p}.description", "Description must be at most 1000 characters.");
            v.Require(l.Quantity > 0, $"{p}.quantity", "Quantity must be greater than zero.");
            v.Require(l.Quantity <= 1_000_000, $"{p}.quantity", "Quantity is too large.");
            v.Require(DocumentCalculator.HasAtMostDecimals(l.Quantity, DocumentCalculator.QuantityDecimals), $"{p}.quantity", "Quantity can have at most 3 decimals.");
            v.Require(l.UnitPrice >= 0, $"{p}.unitPrice", "Unit price cannot be negative.");
            v.Require(l.UnitPrice <= 999_999_999, $"{p}.unitPrice", "Unit price is too large.");
            v.Require(DocumentCalculator.HasAtMostDecimals(l.UnitPrice, DocumentCalculator.MoneyDecimals), $"{p}.unitPrice", "Unit price can have at most 2 decimals.");
            v.Require(Enum.IsDefined(l.TaxCategory), $"{p}.taxCategory", "Unknown tax category.");
            if (l.ItemId is { } itemId)
            {
                v.Require(items.TryGetValue(itemId, out var active) && (active || allowInactiveItems), $"{p}.itemId", "Item not found or inactive.");
            }
        }
    }

    /// <summary>Builds lines with the document discount spread over them, and sets the document's discount and totals.</summary>
    public static void Build<TLine>(
        this IDiscounted document, IReadOnlyList<DocumentLineRequest> requests, DocumentDiscountRequest? discount, decimal vatRate, List<TLine> target)
        where TLine : DocumentLineBase, new()
    {
        var d = discount.ToDiscount();
        var totals = Build(requests, vatRate, target, d);
        document.DiscountKind = d.Kind;
        document.DiscountValue = d.Kind == DiscountKind.None ? 0 : d.Value;
        document.Discount = totals.Discount;
        (document.SubTotal, document.VatTotal, document.Total) = totals;
    }

    /// <summary>Builds line entities with amounts from <see cref="DocumentCalculator"/> and returns the document totals.</summary>
    public static DocumentTotalsDto Build<TLine>(
        IReadOnlyList<DocumentLineRequest> requests, decimal vatRate, List<TLine> target, DocumentDiscount? discount = null)
        where TLine : DocumentLineBase, new()
    {
        var totals = DocumentCalculator.Calculate(requests.Select(r => new LineInput(r.Quantity, r.UnitPrice, r.TaxCategory)), vatRate, discount);
        target.Clear();
        for (var i = 0; i < requests.Count; i++)
        {
            var r = requests[i];
            var a = totals.Lines[i];
            target.Add(new TLine
            {
                LineNo = i + 1,
                ItemId = r.ItemId,
                Description = r.Description.Trim(),
                Quantity = r.Quantity,
                UnitPrice = r.UnitPrice,
                TaxCategory = r.TaxCategory,
                VatRate = a.VatRate,
                Discount = a.Discount,
                Net = a.Net,
                Vat = a.Vat,
                Total = a.Total,
            });
        }

        return new DocumentTotalsDto(totals.SubTotal, totals.VatTotal, totals.Total) { Discount = totals.Discount };
    }

    public static DocumentLineRequest ToRequest(this DocumentLineBase l) => new(l.ItemId, l.Description, l.Quantity, l.UnitPrice, l.TaxCategory);
}
