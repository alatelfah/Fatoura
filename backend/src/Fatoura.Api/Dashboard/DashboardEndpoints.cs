using Fatoura.Api.Auth;
using Fatoura.Api.Data;
using Fatoura.Api.Data.Entities;
using Fatoura.Api.Documents;
using Fatoura.Api.Infrastructure;
using Fatoura.Api.Items;
using Fatoura.Api.Reports;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Fatoura.Api.Dashboard;

public sealed record MonthlyTotals(string Month, decimal Sales, decimal Purchases);

public sealed record RecentInvoiceDto(int Id, string Number, DateOnly Date, string ClientName, decimal Total, decimal Balance, InvoiceStatus Status);

public sealed record AdminDashboardDto(
    ReportPeriod Period,
    decimal Sales,
    decimal Purchases,
    decimal NetProfit,
    decimal OutputVat,
    decimal InputVat,
    int OpenInvoiceCount,
    decimal OpenInvoiceBalance,
    int OpenQuotationCount,
    int LowStockCount,
    List<ItemDto> LowStockItems,
    List<MonthlyTotals> Monthly,
    List<RecentInvoiceDto> RecentInvoices);

public sealed record CashierDashboardDto(
    DateOnly Today, int ShiftInvoiceCount, decimal ShiftSalesNet, decimal ShiftSalesTotal, List<RecentInvoiceDto> RecentInvoices);

public static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/dashboard").WithTags("Dashboard");
        group.MapGet("/admin", Admin).RequireAuthorization(Policies.Admin);
        group.MapGet("/cashier", Cashier).RequireAuthorization(Policies.Staff);
        return group;
    }

    /// <summary>KPIs default to the year to date; the chart always covers the last 12 months.</summary>
    private static async Task<Ok<AdminDashboardDto>> Admin(
        DateOnly? from, DateOnly? to, FatouraDbContext db, ReportService reports, BusinessClock clock, CancellationToken ct)
    {
        var today = clock.Today;
        var period = ReportEndpoints.Period(from ?? new DateOnly(today.Year, 1, 1), to ?? today, clock);
        var pl = await reports.ProfitLossAsync(period, ct);
        var vat = await reports.VatAsync(period, ct);

        var open = await db.Invoices.AsNoTracking().Where(i => i.Status == InvoiceStatus.Issued)
            .Select(i => i.Total - (i.CreditNotes.Sum(c => (decimal?)c.Total) ?? 0) - (i.Payments.Sum(p => (decimal?)p.Amount) ?? 0))
            .Where(balance => balance > 0)
            .ToListAsync(ct);
        var openQuotations = await db.Quotations.AsNoTracking().CountAsync(q =>
            (q.Status == QuotationStatus.Draft || q.Status == QuotationStatus.Sent || q.Status == QuotationStatus.Accepted) && q.ValidUntil >= today, ct);

        var lowStock = db.Items.AsNoTracking().Where(i => i.IsActive && i.TrackStock && i.StockQty <= i.ReorderLevel);
        var lowStockCount = await lowStock.CountAsync(ct);
        var lowStockItems = await lowStock.OrderBy(i => i.StockQty - i.ReorderLevel).Take(10)
            .Select(i => new ItemDto(i.Id, i.Type, i.Name, i.Description, i.UnitPrice, i.TaxCategory, i.TrackStock, i.StockQty, i.ReorderLevel, i.AvgCost, true, i.IsActive))
            .ToListAsync(ct);

        var chartStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-11);
        var chart = await reports.ProfitLossAsync(new ReportPeriod(chartStart, today), ct);

        var recent = await Recent(db.Invoices.AsNoTracking(), ct);
        return TypedResults.Ok(new AdminDashboardDto(
            period, pl.NetSales, pl.TotalPurchases, pl.NetProfit, vat.OutputVat, vat.InputVat, open.Count, open.Sum(), openQuotations,
            lowStockCount, lowStockItems, chart.Monthly.Select(m => new MonthlyTotals(m.Month, m.Sales, m.Purchases)).ToList(), recent));
    }

    /// <summary>The signed-in user's own sales today (Asia/Dubai calendar day) and latest invoices.</summary>
    private static async Task<Ok<CashierDashboardDto>> Cashier(FatouraDbContext db, ICurrentUser user, BusinessClock clock, CancellationToken ct)
    {
        var me = user.RequireId();
        var today = clock.Today;
        var shift = await db.Invoices.AsNoTracking()
            .Where(i => i.CreatedById == me && i.Date == today && i.Status == InvoiceStatus.Issued)
            .Select(i => new { i.SubTotal, i.Total }).ToListAsync(ct);
        var recent = await Recent(db.Invoices.AsNoTracking().Where(i => i.CreatedById == me), ct);
        return TypedResults.Ok(new CashierDashboardDto(today, shift.Count, shift.Sum(s => s.SubTotal), shift.Sum(s => s.Total), recent));
    }

    private static Task<List<RecentInvoiceDto>> Recent(IQueryable<Invoice> q, CancellationToken ct) =>
        q.OrderByDescending(i => i.CreatedAt).Take(10)
            .Select(i => new RecentInvoiceDto(
                i.Id, i.Number, i.Date, i.ClientSnapshot.Name, i.Total,
                i.Status == InvoiceStatus.Void ? 0 : i.Total - (i.CreditNotes.Sum(c => (decimal?)c.Total) ?? 0) - (i.Payments.Sum(p => (decimal?)p.Amount) ?? 0),
                i.Status))
            .ToListAsync(ct);
}
