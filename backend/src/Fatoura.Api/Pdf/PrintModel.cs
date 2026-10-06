namespace Fatoura.Api.Pdf;

/// <summary>Everything a printed sales document needs; built from an invoice, quotation or credit note.</summary>
public sealed record PrintDocument(
    string Title,
    string NumberLabel,
    string Number,
    DateOnly Date,
    PrintCompany Company,
    PrintParty Client,
    IReadOnlyList<PrintInfo> ExtraInfo,
    IReadOnlyList<PrintLine> Lines,
    decimal SubTotal,
    string VatLabel,
    decimal VatTotal,
    decimal Total,
    PrintTerms Terms,
    bool IsVoid);

public sealed record PrintCompany(string Name, string Address, string Phone, string Email, string Website, string Trn, byte[]? Logo, byte[]? Stamp);

public sealed record PrintParty(string Name, string Address, string Phone, string Trn);

public sealed record PrintInfo(string Label, string Value);

public sealed record PrintLine(int No, string Description, decimal Quantity, decimal UnitPrice, decimal Vat, decimal Amount);

public sealed record PrintTerms(string PaymentTerms, string CompletionOfWork, IReadOnlyList<string> Notes, string ClosingText);
