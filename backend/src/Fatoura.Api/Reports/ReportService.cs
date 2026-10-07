using Fatoura.Api.Data;
using Fatoura.Api.Data.Entities;
using Fatoura.Api.Settings;
using Fatoura.Domain.Tax;
using Microsoft.EntityFrameworkCore;

namespace Fatoura.Api.Reports;

/// <summary>
/// Report figures, all in AED (documents in other currencies use their AED equivalents). Void invoices are excluded
/// everywhere; credit notes count in the period of their own date and reduce sales and output VAT. All "net" amounts exclude VAT.
/// </summary>
public sealed class ReportService(FatouraDbContext db, SettingsService settings)
{
    public async Task<SalesReportDto> SalesAsync(ReportPeriod period, Guid? cashierId, int? clientId, CancellationToken ct)
    {
        var invoices = db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Issued && i.Date >= period.From && i.Date <= period.To);
        var credits = db.CreditNotes.AsNoTracking().Where(c => c.Date >= period.From && c.Date <= period.To);
        if (cashierId is { } u)
        {
            invoices = invoices.Where(i => i.CreatedById == u);
            credits = credits.Where(c => c.CreatedById == u);
        }

        if (clientId is { } cl)
        {
            invoices = invoices.Where(i => i.ClientId == cl);
            credits = credits.Where(c => c.Invoice!.ClientId == cl);
        }

        var invoiceRows = await invoices
            .Select(i => new SalesReportRow(SalesDocumentKind.Invoice, i.Id, i.Number, i.Date, i.ClientSnapshot.Name, i.CreatedBy!.DisplayName, i.Currency, i.SubTotalAed, i.VatTotalAed, i.TotalAed))
            .ToListAsync(ct);
        var creditRows = await credits
            .Select(c => new SalesReportRow(SalesDocumentKind.CreditNote, c.Id, c.Number, c.Date, c.ClientSnapshot.Name, c.CreatedBy!.DisplayName, c.Currency, -c.SubTotalAed, -c.VatTotalAed, -c.TotalAed))
            .ToListAsync(ct);
        var cashierIds = await invoices.Select(i => new { i.CreatedById, i.CreatedBy!.DisplayName, SubTotal = i.SubTotalAed, VatTotal = i.VatTotalAed, Total = i.TotalAed }).ToListAsync(ct);

        var summary = new SalesSummary(
            invoiceRows.Count, invoiceRows.Sum(r => r.Net), invoiceRows.Sum(r => r.Vat), invoiceRows.Sum(r => r.Total),
            creditRows.Count, -creditRows.Sum(r => r.Net), -creditRows.Sum(r => r.Vat), -creditRows.Sum(r => r.Total),
            invoiceRows.Sum(r => r.Net) + creditRows.Sum(r => r.Net),
            invoiceRows.Sum(r => r.Vat) + creditRows.Sum(r => r.Vat),
            invoiceRows.Sum(r => r.Total) + creditRows.Sum(r => r.Total));

        var byCashier = cashierIds.GroupBy(x => new { x.CreatedById, x.DisplayName })
            .Select(g => new SalesByCashier(g.Key.CreatedById, g.Key.DisplayName, g.Count(), g.Sum(x => x.SubTotal), g.Sum(x => x.VatTotal), g.Sum(x => x.Total)))
            .OrderByDescending(x => x.Total).ToList();

        var rows = invoiceRows.Concat(creditRows).OrderBy(r => r.Date).ThenBy(r => r.Kind).ThenBy(r => r.Number, StringComparer.Ordinal).ToList();
        return new SalesReportDto(period, summary, byCashier, rows);
    }

    public async Task<PurchasesReportDto> PurchasesAsync(ReportPeriod period, int? supplierId, CancellationToken ct)
    {
        var q = db.PurchaseInvoices.AsNoTracking().Where(p => p.Date >= period.From && p.Date <= period.To);
        if (supplierId is { } s)
        {
            q = q.Where(p => p.SupplierId == s);
        }

        var rows = await q.OrderBy(p => p.Date).ThenBy(p => p.Id)
            .Select(p => new PurchaseReportRow(p.Id, p.Number, p.SupplierInvoiceNo, p.Date, p.SupplierSnapshot.Name, p.Currency, p.SubTotalAed, p.VatTotalAed, p.TotalAed))
            .ToListAsync(ct);
        var bySupplier = (await q.Select(p => new { p.SupplierId, p.SupplierSnapshot.Name, SubTotal = p.SubTotalAed, VatTotal = p.VatTotalAed, Total = p.TotalAed }).ToListAsync(ct))
            .GroupBy(p => p.SupplierId)
            .Select(g => new PurchasesBySupplier(g.Key, g.First().Name, g.Count(), g.Sum(x => x.SubTotal), g.Sum(x => x.VatTotal), g.Sum(x => x.Total)))
            .OrderByDescending(x => x.Total).ToList();
        return new PurchasesReportDto(period, rows.Count, rows.Sum(r => r.Net), rows.Sum(r => r.Vat), rows.Sum(r => r.Total), bySupplier, rows);
    }

    public async Task<ProfitLossDto> ProfitLossAsync(ReportPeriod period, CancellationToken ct)
    {
        var sales = await db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Issued && i.Date >= period.From && i.Date <= period.To)
            .Select(i => new { i.Date, SubTotal = i.SubTotalAed }).ToListAsync(ct);
        var credits = await db.CreditNotes.AsNoTracking()
            .Where(c => c.Date >= period.From && c.Date <= period.To)
            .Select(c => new { c.Date, SubTotal = c.SubTotalAed }).ToListAsync(ct);
        var purchaseLines = await db.PurchaseLines.AsNoTracking()
            .Join(db.PurchaseInvoices, l => l.PurchaseInvoiceId, p => p.Id, (l, p) => new { p.Date, l.ItemId, l.ExpenseCategory, Net = l.NetAed })
            .Where(x => x.Date >= period.From && x.Date <= period.To)
            .ToListAsync(ct);

        var grossSales = sales.Sum(s => s.SubTotal);
        var creditTotal = credits.Sum(c => c.SubTotal);
        var inventory = purchaseLines.Where(l => l.ItemId is not null).Sum(l => l.Net);
        var expenses = purchaseLines.Where(l => l.ItemId is null)
            .GroupBy(l => string.IsNullOrWhiteSpace(l.ExpenseCategory) ? "General" : l.ExpenseCategory)
            .Select(g => new ExpenseLine(g.Key, g.Sum(l => l.Net))).OrderByDescending(e => e.Amount).ToList();
        var totalPurchases = inventory + expenses.Sum(e => e.Amount);

        static string Month(DateOnly d) => $"{d.Year:D4}-{d.Month:D2}";
        var monthly = new List<MonthlyProfit>();
        for (var m = new DateOnly(period.From.Year, period.From.Month, 1); m <= period.To; m = m.AddMonths(1))
        {
            var key = Month(m);
            var s = sales.Where(x => Month(x.Date) == key).Sum(x => x.SubTotal) - credits.Where(x => Month(x.Date) == key).Sum(x => x.SubTotal);
            var p = purchaseLines.Where(x => Month(x.Date) == key).Sum(x => x.Net);
            monthly.Add(new MonthlyProfit(key, s, p, s - p));
        }

        var netSales = grossSales - creditTotal;
        return new ProfitLossDto(period, grossSales, creditTotal, netSales, inventory, expenses, totalPurchases, netSales - totalPurchases, monthly);
    }

    /// <summary>VAT 201 layout: boxes 1 (by emirate), 4, 5, 8 for outputs; 9, 11 for inputs; 12–14 for the net position.</summary>
    public async Task<VatReportDto> VatAsync(ReportPeriod period, CancellationToken ct)
    {
        var company = await settings.GetAsync(ct);
        var sales = await db.InvoiceLines.AsNoTracking()
            .Join(db.Invoices, l => l.InvoiceId, i => i.Id, (l, i) => new { i.Date, i.Status, l.TaxCategory, Net = l.NetAed, Vat = l.VatAed })
            .Where(x => x.Status == InvoiceStatus.Issued && x.Date >= period.From && x.Date <= period.To)
            .GroupBy(x => x.TaxCategory).Select(g => new { Category = g.Key, Net = g.Sum(x => x.Net), Vat = g.Sum(x => x.Vat) })
            .ToListAsync(ct);
        var credits = await db.CreditNoteLines.AsNoTracking()
            .Join(db.CreditNotes, l => l.CreditNoteId, c => c.Id, (l, c) => new { c.Date, l.TaxCategory, Net = l.NetAed, Vat = l.VatAed })
            .Where(x => x.Date >= period.From && x.Date <= period.To)
            .GroupBy(x => x.TaxCategory).Select(g => new { Category = g.Key, Net = g.Sum(x => x.Net), Vat = g.Sum(x => x.Vat) })
            .ToListAsync(ct);
        var inputLines = db.PurchaseLines.AsNoTracking()
            .Join(db.PurchaseInvoices, l => l.PurchaseInvoiceId, p => p.Id, (l, p) => new { p.Date, l.TaxCategory, Net = l.NetAed, Vat = l.VatAed })
            .Where(x => x.Date >= period.From && x.Date <= period.To && x.TaxCategory == TaxCategory.Standard);
        var inputNet = await inputLines.SumAsync(x => (decimal?)x.Net, ct) ?? 0;
        var inputVat = await inputLines.SumAsync(x => (decimal?)x.Vat, ct) ?? 0;

        (decimal Net, decimal Vat) Output(TaxCategory c)
        {
            var s = sales.SingleOrDefault(x => x.Category == c);
            var cr = credits.SingleOrDefault(x => x.Category == c);
            return ((s?.Net ?? 0) - (cr?.Net ?? 0), (s?.Vat ?? 0) - (cr?.Vat ?? 0));
        }

        var standard = Output(TaxCategory.Standard);
        var zero = Output(TaxCategory.ZeroRated);
        var exempt = Output(TaxCategory.Exempt);

        var boxes = new List<VatBox>();
        var emirates = new[]
        {
            ("1a", Emirate.AbuDhabi, "Abu Dhabi"), ("1b", Emirate.Dubai, "Dubai"), ("1c", Emirate.Sharjah, "Sharjah"),
            ("1d", Emirate.Ajman, "Ajman"), ("1e", Emirate.UmmAlQuwain, "Umm Al Quwain"), ("1f", Emirate.RasAlKhaimah, "Ras Al Khaimah"),
            ("1g", Emirate.Fujairah, "Fujairah"),
        };
        foreach (var (box, emirate, name) in emirates)
        {
            var mine = emirate == company.Emirate;
            boxes.Add(new VatBox(box, $"Standard rated supplies in {name}", mine ? standard.Net : 0, mine ? standard.Vat : 0));
        }

        boxes.Add(new VatBox("4", "Zero rated supplies", zero.Net, 0));
        boxes.Add(new VatBox("5", "Exempt supplies", exempt.Net, 0));
        boxes.Add(new VatBox("8", "Totals (supplies and output tax)", standard.Net + zero.Net + exempt.Net, standard.Vat));
        boxes.Add(new VatBox("9", "Standard rated expenses", inputNet, inputVat));
        boxes.Add(new VatBox("11", "Totals (expenses and recoverable tax)", inputNet, inputVat));
        boxes.Add(new VatBox("12", "Total value of due tax for the period", 0, standard.Vat));
        boxes.Add(new VatBox("13", "Total value of recoverable tax for the period", 0, inputVat));
        boxes.Add(new VatBox("14", "Payable tax for the period", 0, standard.Vat - inputVat));
        return new VatReportDto(period, company.Emirate, boxes, standard.Vat, inputVat, standard.Vat - inputVat);
    }
}
