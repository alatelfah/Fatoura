namespace Fatoura.Api.Pdf;

/// <summary>
/// Everything a printed sales document needs; built from an invoice, quotation or credit note.
/// <c>SubTotal</c> is after <c>Discount</c>; with a discount the totals show the sub total before it, the discount
/// and the taxable amount, so the invoice states the discount as the FTA requires. Amounts are in AED unless
/// <c>Currency</c> is given.
/// </summary>
public sealed record PrintDocument(
    string Title,
    string NumberLabel,
    string Number,
    DateOnly Date,
    PrintCompany Company,
    PrintParty Client,
    IReadOnlyList<PrintInfo> ExtraInfo,
    IReadOnlyList<PrintLine> Lines,
    decimal Discount,
    decimal SubTotal,
    string VatLabel,
    decimal VatTotal,
    decimal Total,
    PrintTerms Terms,
    bool IsVoid,
    PrintCurrency? Currency = null);

/// <summary>
/// A document in another currency: amounts print in <paramref name="Code"/>, with the rate and the VAT and total in AED
/// (the FTA requires the VAT amount in AED).
/// </summary>
public sealed record PrintCurrency(string Code, decimal Rate, decimal VatTotalAed, decimal TotalAed);

public sealed record PrintCompany(string Name, string Address, string Phone, string Email, string Website, string Trn, byte[]? Logo, byte[]? Stamp);

public sealed record PrintParty(string Name, string Address, string Phone, string Trn);

public sealed record PrintInfo(string Label, string Value);

/// <summary>When the document has a discount, <paramref name="Discount"/> is the line's share and <paramref name="Amount"/> is after it.</summary>
public sealed record PrintLine(int No, string Description, decimal Quantity, decimal UnitPrice, decimal Discount, decimal Vat, decimal Amount);

public sealed record PrintTerms(string PaymentTerms, string CompletionOfWork, IReadOnlyList<string> Notes, string ClosingText);
