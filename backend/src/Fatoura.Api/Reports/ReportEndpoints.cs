using Fatoura.Api.Auth;
using Fatoura.Api.Infrastructure;
using Fatoura.Api.Settings;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Fatoura.Api.Reports;

public static class ReportEndpoints
{
    public static RouteGroupBuilder MapReportEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/reports").WithTags("Reports");

        // Cashiers may run the sales report for their own sales only; profit, purchases and VAT are Admin only (BRD §2).
        group.MapGet("/sales", Sales).RequireAuthorization(Policies.Staff);
        group.MapGet("/purchases", Purchases).RequireAuthorization(Policies.Admin);
        group.MapGet("/profit-loss", ProfitLoss).RequireAuthorization(Policies.Admin);
        group.MapGet("/vat", Vat).RequireAuthorization(Policies.Admin);

        group.MapGet("/sales/export", async (DateOnly? from, DateOnly? to, Guid? cashierId, int? clientId, ReportService reports, SettingsService settings, ICurrentUser user, BusinessClock clock, CancellationToken ct) =>
        {
            var period = Period(from, to, clock);
            var report = await reports.SalesAsync(period, user.IsAdmin ? cashierId : user.RequireId(), clientId, ct);
            return Xlsx(ReportExcel.Sales(report, ReportExcel.CompanyName(await settings.GetAsync(ct))), ReportExcel.FileName("sales", period));
        }).RequireAuthorization(Policies.Staff);
        group.MapGet("/purchases/export", async (DateOnly? from, DateOnly? to, int? supplierId, ReportService reports, SettingsService settings, BusinessClock clock, CancellationToken ct) =>
        {
            var period = Period(from, to, clock);
            var report = await reports.PurchasesAsync(period, supplierId, ct);
            return Xlsx(ReportExcel.Purchases(report, ReportExcel.CompanyName(await settings.GetAsync(ct))), ReportExcel.FileName("purchases", period));
        }).RequireAuthorization(Policies.Admin);
        group.MapGet("/profit-loss/export", async (DateOnly? from, DateOnly? to, ReportService reports, SettingsService settings, BusinessClock clock, CancellationToken ct) =>
        {
            var period = Period(from, to, clock);
            var report = await reports.ProfitLossAsync(period, ct);
            return Xlsx(ReportExcel.ProfitLoss(report, ReportExcel.CompanyName(await settings.GetAsync(ct))), ReportExcel.FileName("profit-loss", period));
        }).RequireAuthorization(Policies.Admin);
        group.MapGet("/vat/export", async (DateOnly? from, DateOnly? to, ReportService reports, SettingsService settings, BusinessClock clock, CancellationToken ct) =>
        {
            var period = Period(from, to, clock);
            var report = await reports.VatAsync(period, ct);
            return Xlsx(ReportExcel.Vat(report, ReportExcel.CompanyName(await settings.GetAsync(ct))), ReportExcel.FileName("vat", period));
        }).RequireAuthorization(Policies.Admin);
        return group;
    }

    /// <summary>Defaults to the current month to date; at most 5 years.</summary>
    public static ReportPeriod Period(DateOnly? from, DateOnly? to, BusinessClock clock)
    {
        var today = clock.Today;
        var end = to ?? today;
        var start = from ?? new DateOnly(end.Year, end.Month, 1);
        new FieldValidator()
            .Require(start <= end, "from", "The start date must be on or before the end date.")
            .Require(end.DayNumber - start.DayNumber <= 366 * 5, "to", "The period can be at most 5 years.")
            .ThrowIfInvalid();
        return new ReportPeriod(start, end);
    }

    private static FileContentHttpResult Xlsx(byte[] bytes, string fileName) => TypedResults.File(bytes, ReportExcel.ContentType, fileName);

    private static async Task<Ok<SalesReportDto>> Sales(
        DateOnly? from, DateOnly? to, Guid? cashierId, int? clientId, ReportService reports, ICurrentUser user, BusinessClock clock, CancellationToken ct)
    {
        var cashier = user.IsAdmin ? cashierId : user.RequireId();
        return TypedResults.Ok(await reports.SalesAsync(Period(from, to, clock), cashier, clientId, ct));
    }

    private static async Task<Ok<PurchasesReportDto>> Purchases(
        DateOnly? from, DateOnly? to, int? supplierId, ReportService reports, BusinessClock clock, CancellationToken ct) =>
        TypedResults.Ok(await reports.PurchasesAsync(Period(from, to, clock), supplierId, ct));

    private static async Task<Ok<ProfitLossDto>> ProfitLoss(DateOnly? from, DateOnly? to, ReportService reports, BusinessClock clock, CancellationToken ct) =>
        TypedResults.Ok(await reports.ProfitLossAsync(Period(from, to, clock), ct));

    private static async Task<Ok<VatReportDto>> Vat(DateOnly? from, DateOnly? to, ReportService reports, BusinessClock clock, CancellationToken ct) =>
        TypedResults.Ok(await reports.VatAsync(Period(from, to, clock), ct));
}
