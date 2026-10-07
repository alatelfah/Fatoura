using System.Globalization;
using Fatoura.Api.Data;
using Fatoura.Api.Data.Entities;
using Fatoura.Api.Infrastructure;
using Fatoura.Api.Settings;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Fatoura.Api.Pdf;

public sealed class PdfService(FatouraDbContext db, SettingsService settingsService)
{
    private static readonly Lazy<bool> Initialized = new(Initialize);

    /// <summary>
    /// QuestPDF Community licence (free for organisations under USD 1M annual revenue — see docs/DECISIONS.md).
    /// Fonts are embedded resources so output is identical on every machine; system fonts are not used.
    /// </summary>
    private static bool Initialize()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = false;
        var assembly = typeof(PdfService).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("Fonts.", StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            QuestPDF.Drawing.FontManager.RegisterFontFromStream(stream);
        }

        return true;
    }

    public static byte[] Render(PrintDocument document)
    {
        _ = Initialized.Value;
        return new SalesDocumentPdf(document).GeneratePdf();
    }

    public async Task<(byte[] Pdf, string FileName)> InvoiceAsync(int id, CancellationToken ct)
    {
        var i = await db.Invoices.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Invoice");
        var settings = await settingsService.GetAsync(ct);
        var extra = new List<PrintInfo>();
        if (!string.IsNullOrWhiteSpace(i.QuotationNumber))
        {
            extra.Add(new PrintInfo("Quotation Ref:", i.QuotationNumber));
        }

        var doc = new PrintDocument(
            "Tax Invoice", "Invoice No:", i.Number, i.Date, Company(i.CompanySnapshot, settings), Party(i.ClientSnapshot), extra,
            Lines(i.Lines), i.Discount, i.SubTotal, VatLabel(settings.VatRate), i.VatTotal, i.Total, Terms(i), i.Status == InvoiceStatus.Void, Currency(i));
        return (Render(doc), FileName("Invoice", i.Number));
    }

    public async Task<(byte[] Pdf, string FileName)> QuotationAsync(int id, CancellationToken ct)
    {
        var q = await db.Quotations.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Quotation");
        var settings = await settingsService.GetAsync(ct);
        var doc = new PrintDocument(
            "Quotation", "Quotation No:", q.Number, q.Date, Company(CompanySnapshot.From(settings), settings), Party(q.ClientSnapshot),
            [new PrintInfo("Valid Until:", SalesDocumentPdf.Date(q.ValidUntil))],
            Lines(q.Lines), q.Discount, q.SubTotal, VatLabel(settings.VatRate), q.VatTotal, q.Total, Terms(q), IsVoid: false, Currency(q));
        return (Render(doc), FileName("Quotation", q.Number));
    }

    public async Task<(byte[] Pdf, string FileName)> CreditNoteAsync(int id, CancellationToken ct)
    {
        var c = await db.CreditNotes.AsNoTracking().Include(x => x.Lines).Include(x => x.Invoice)
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Credit note");
        var settings = await settingsService.GetAsync(ct);
        var extra = new List<PrintInfo>
        {
            new("Against Invoice:", c.Invoice!.Number),
            new("Invoice Date:", SalesDocumentPdf.Date(c.Invoice.Date)),
        };

        // A credit note's footer states why it was issued instead of commercial terms.
        var terms = new PrintTerms(string.Empty, string.Empty, [], $"Reason: {c.Reason}");
        var doc = new PrintDocument(
            "Tax Credit Note", "Credit Note No:", c.Number, c.Date, Company(c.CompanySnapshot, settings), Party(c.ClientSnapshot), extra,
            Lines(c.Lines), c.Discount, c.SubTotal, VatLabel(settings.VatRate), c.VatTotal, c.Total, terms, IsVoid: false, Currency(c));
        return (Render(doc), FileName("CreditNote", c.Number));
    }

    private static PrintCurrency? Currency(ICurrencyDocument d) =>
        d.Currency == Domain.Documents.CurrencyConverter.Base ? null : new PrintCurrency(d.Currency, d.ExchangeRate, d.VatTotalAed, d.TotalAed);

    public static string VatLabel(decimal rate) => $"VAT {(rate * 100).ToString("0.##", CultureInfo.InvariantCulture)}%";

    private static PrintCompany Company(CompanySnapshot c, CompanySettings settings) =>
        new(c.Name, c.Address, c.Phone, c.Email, c.Website, c.Trn, settings.Logo, settings.Stamp);

    private static PrintParty Party(PartySnapshot p) => new(p.Name, p.Address, p.Phone, p.Trn);

    private static List<PrintLine> Lines(IEnumerable<DocumentLineBase> lines) =>
        lines.OrderBy(l => l.LineNo).Select(l => new PrintLine(l.LineNo, l.Description, l.Quantity, l.UnitPrice, l.Discount, l.Vat, l.Total)).ToList();

    private static PrintTerms Terms(IDocumentTerms t) => new(
        t.PaymentTerms,
        t.CompletionOfWork,
        t.Notes.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(n => System.Text.RegularExpressions.Regex.Replace(n, @"^\d+[.)]\s*", string.Empty)).ToList(),
        t.ClosingText);

    /// <summary>Document numbers may contain "/" (e.g. INV/SEP/260001); make a safe file name.</summary>
    public static string FileName(string kind, string number) =>
        $"{kind}-{string.Concat(number.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-'))}.pdf";
}
