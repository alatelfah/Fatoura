using ClosedXML.Excel;
using Fatoura.Api.Data.Entities;

namespace Fatoura.Api.Reports;

/// <summary>Excel (.xlsx) versions of the reports, for accountants and FTA filing.</summary>
public static class ReportExcel
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Money = "#,##0.00";

    public static byte[] Sales(SalesReportDto r, string company)
    {
        using var wb = new XLWorkbook();
        var ws = Sheet(wb, "Sales", "Sales Report", company, r.Period);
        var s = r.Summary;
        var row = Summary(ws, 5,
        [
            ("Invoices", s.InvoiceCount), ("Invoices net", s.InvoicesNet), ("Invoices VAT", s.InvoicesVat), ("Invoices total", s.InvoicesTotal),
            ("Credit notes", s.CreditNoteCount), ("Credit notes net", s.CreditNotesNet), ("Credit notes VAT", s.CreditNotesVat),
            ("Credit notes total", s.CreditNotesTotal), ("Net sales (excl. VAT)", s.NetSales), ("Net VAT", s.NetVat), ("Net total", s.NetTotal),
        ]);
        row = Table(ws, row + 1, ["Type", "Number", "Date", "Client", "Cashier", "Net (AED)", "VAT (AED)", "Total (AED)"],
            r.Rows.Select(x => new object[] { x.Kind == SalesDocumentKind.Invoice ? "Invoice" : "Credit note", x.Number, x.Date, x.ClientName, x.CashierName, x.Net, x.Vat, x.Total }), [6, 7, 8]);
        Table(ws, row + 1, ["Cashier", "Invoices", "Net (AED)", "VAT (AED)", "Total (AED)"],
            r.ByCashier.Select(x => new object[] { x.CashierName, x.InvoiceCount, x.Net, x.Vat, x.Total }), [3, 4, 5]);
        return Save(wb, ws);
    }

    public static byte[] Purchases(PurchasesReportDto r, string company)
    {
        using var wb = new XLWorkbook();
        var ws = Sheet(wb, "Purchases", "Purchases Report", company, r.Period);
        var row = Summary(ws, 5, [("Purchases", r.Count), ("Net", r.Net), ("VAT (input)", r.Vat), ("Total", r.Total)]);
        row = Table(ws, row + 1, ["Number", "Supplier invoice", "Date", "Supplier", "Net (AED)", "VAT (AED)", "Total (AED)"],
            r.Rows.Select(x => new object[] { x.Number, x.SupplierInvoiceNo, x.Date, x.SupplierName, x.Net, x.Vat, x.Total }), [5, 6, 7]);
        Table(ws, row + 1, ["Supplier", "Purchases", "Net (AED)", "VAT (AED)", "Total (AED)"],
            r.BySupplier.Select(x => new object[] { x.SupplierName, x.Count, x.Net, x.Vat, x.Total }), [3, 4, 5]);
        return Save(wb, ws);
    }

    public static byte[] ProfitLoss(ProfitLossDto r, string company)
    {
        using var wb = new XLWorkbook();
        var ws = Sheet(wb, "Profit & Loss", "Profit & Loss", company, r.Period);
        var lines = new List<(string, object)> { ("Sales (excl. VAT)", r.Sales), ("Less: credit notes", -r.CreditNotes), ("Net sales", r.NetSales), ("Inventory purchases", r.InventoryPurchases) };
        lines.AddRange(r.Expenses.Select(e => ($"Expense: {e.Category}", (object)e.Amount)));
        lines.Add(("Total purchases & costs", r.TotalPurchases));
        lines.Add(("Net profit", r.NetProfit));
        var row = Summary(ws, 5, lines);
        Table(ws, row + 1, ["Month", "Net sales (AED)", "Purchases (AED)", "Profit (AED)"],
            r.Monthly.Select(m => new object[] { m.Month, m.Sales, m.Purchases, m.Profit }), [2, 3, 4]);
        return Save(wb, ws);
    }

    public static byte[] Vat(VatReportDto r, string company)
    {
        using var wb = new XLWorkbook();
        var ws = Sheet(wb, "VAT", "VAT Return (VAT 201)", company, r.Period);
        ws.Cell(4, 1).Value = $"Emirate: {r.Emirate}";
        var row = Table(ws, 6, ["Box", "Description", "Amount (AED)", "VAT (AED)"],
            r.Boxes.Select(b => new object[] { b.Box, b.Label, b.Amount, b.Vat }), [3, 4]);
        Summary(ws, row + 1, [("Output VAT", r.OutputVat), ("Input VAT", r.InputVat), ("Net VAT payable (refundable if negative)", r.NetVatPayable)]);
        return Save(wb, ws);
    }

    private static IXLWorksheet Sheet(XLWorkbook wb, string name, string title, string company, ReportPeriod period)
    {
        var ws = wb.Worksheets.Add(name);
        ws.Cell(1, 1).Value = company;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = title;
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Font.FontSize = 14;
        ws.Cell(3, 1).Value = $"{period.From:yyyy-MM-dd} to {period.To:yyyy-MM-dd}";
        return ws;
    }

    private static int Summary(IXLWorksheet ws, int row, IEnumerable<(string Label, object Value)> lines)
    {
        foreach (var (label, value) in lines)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.Bold = true;
            Set(ws.Cell(row, 2), value);
            if (value is decimal)
            {
                ws.Cell(row, 2).Style.NumberFormat.Format = Money;
            }

            row++;
        }

        return row;
    }

    private static int Table(IXLWorksheet ws, int row, string[] headers, IEnumerable<object[]> rows, int[] moneyColumns)
    {
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(row, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#DDEBF7");
        }

        var first = row + 1;
        foreach (var values in rows)
        {
            row++;
            for (var c = 0; c < values.Length; c++)
            {
                Set(ws.Cell(row, c + 1), values[c]);
            }
        }

        foreach (var c in moneyColumns)
        {
            ws.Range(first, c, Math.Max(first, row), c).Style.NumberFormat.Format = Money;
        }

        return row + 1;
    }

    private static void Set(IXLCell cell, object value)
    {
        switch (value)
        {
            case decimal d:
                cell.Value = d;
                break;
            case int i:
                cell.Value = i;
                break;
            case DateOnly date:
                cell.Value = date.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = "dd-MMM-yyyy";
                break;
            default:
                cell.Value = value?.ToString() ?? string.Empty;
                break;
        }
    }

    private static byte[] Save(XLWorkbook wb, IXLWorksheet ws)
    {
        ws.Columns().AdjustToContents(1, 200, 8, 60);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    internal static string FileName(string report, ReportPeriod p) => $"{report}-{p.From:yyyyMMdd}-{p.To:yyyyMMdd}.xlsx";

    internal static string CompanyName(CompanySettings s) => string.IsNullOrWhiteSpace(s.Name) ? "Company" : s.Name;
}
