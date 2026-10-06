using Fatoura.Api.Data.Entities;

namespace Fatoura.Api.Reports;

public sealed record ReportPeriod(DateOnly From, DateOnly To);

public enum SalesDocumentKind
{
    Invoice = 0,
    CreditNote = 1,
}

/// <summary>One sales document; credit notes carry negative amounts so rows sum to net sales.</summary>
public sealed record SalesReportRow(
    SalesDocumentKind Kind, int Id, string Number, DateOnly Date, string ClientName, string CashierName, decimal Net, decimal Vat, decimal Total);

public sealed record SalesByCashier(Guid CashierId, string CashierName, int InvoiceCount, decimal Net, decimal Vat, decimal Total);

public sealed record SalesSummary(
    int InvoiceCount,
    decimal InvoicesNet,
    decimal InvoicesVat,
    decimal InvoicesTotal,
    int CreditNoteCount,
    decimal CreditNotesNet,
    decimal CreditNotesVat,
    decimal CreditNotesTotal,
    decimal NetSales,
    decimal NetVat,
    decimal NetTotal);

public sealed record SalesReportDto(ReportPeriod Period, SalesSummary Summary, List<SalesByCashier> ByCashier, List<SalesReportRow> Rows);

public sealed record PurchaseReportRow(int Id, string Number, string SupplierInvoiceNo, DateOnly Date, string SupplierName, decimal Net, decimal Vat, decimal Total);

public sealed record PurchasesBySupplier(int SupplierId, string SupplierName, int Count, decimal Net, decimal Vat, decimal Total);

public sealed record PurchasesReportDto(
    ReportPeriod Period, int Count, decimal Net, decimal Vat, decimal Total, List<PurchasesBySupplier> BySupplier, List<PurchaseReportRow> Rows);

public sealed record ExpenseLine(string Category, decimal Amount);

public sealed record MonthlyProfit(string Month, decimal Sales, decimal Purchases, decimal Profit);

/// <summary>BRD §3.7: Profit = Sales − Purchases/costs, all excluding VAT.</summary>
public sealed record ProfitLossDto(
    ReportPeriod Period,
    decimal Sales,
    decimal CreditNotes,
    decimal NetSales,
    decimal InventoryPurchases,
    List<ExpenseLine> Expenses,
    decimal TotalPurchases,
    decimal NetProfit,
    List<MonthlyProfit> Monthly);

/// <summary>A line of the FTA VAT 201 return.</summary>
public sealed record VatBox(string Box, string Label, decimal Amount, decimal Vat);

public sealed record VatReportDto(
    ReportPeriod Period, Emirate Emirate, List<VatBox> Boxes, decimal OutputVat, decimal InputVat, decimal NetVatPayable);
