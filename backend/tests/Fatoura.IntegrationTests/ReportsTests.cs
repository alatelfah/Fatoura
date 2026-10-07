using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fatoura.Api.Contacts;
using Fatoura.Api.Dashboard;
using Fatoura.Api.Documents;
using Fatoura.Api.Items;
using Fatoura.Api.Purchases;
using Fatoura.Api.Reports;
using Fatoura.IntegrationTests.Infrastructure;

namespace Fatoura.IntegrationTests;

/// <summary>Replays spec/report-scenario.json through the API once per test class.</summary>
public sealed class ReportScenarioFixture : IAsyncLifetime
{
    public static readonly JsonElement Spec = LoadSpec();

    public ApiFactory Api { get; } = new();

    public HttpClient Admin { get; private set; } = null!;

    public ItemDto Dishwasher => _dishwasher;

    public Dictionary<string, InvoiceDto> Invoices => _invoices;

    private HttpClient _admin = null!;
    private ItemDto _dishwasher = null!;
    private readonly Dictionary<string, InvoiceDto> _invoices = [];

    public async ValueTask InitializeAsync()
    {
        await Api.InitializeAsync();
        var api = Api;
        _admin = Admin = await api.AdminAsync();
        await Scenario.ConfigureCompanyAsync(_admin);
        _dishwasher = await Scenario.DishwasherAsync(_admin);

        var suppliers = new Dictionary<string, int>();
        foreach (var p in Spec.GetProperty("purchases").EnumerateArray())
        {
            var name = p.GetProperty("supplier").GetString()!;
            if (!suppliers.TryGetValue(name, out var supplierId))
            {
                var s = await (await _admin.PostAsJsonAsync("/api/suppliers", new { name })).ReadAsync<SupplierDto>(HttpStatusCode.Created);
                suppliers[name] = supplierId = s.Id;
            }

            var lines = p.GetProperty("lines").EnumerateArray().Select(l => new
            {
                itemId = l.TryGetProperty("item", out _) ? _dishwasher.Id : (int?)null,
                description = l.GetProperty("description").GetString(),
                expenseCategory = l.TryGetProperty("expense", out var e) ? e.GetString() : null,
                quantity = Dec(l, "qty"),
                unitPrice = Dec(l, "unitPrice"),
                taxCategory = l.GetProperty("tax").GetString(),
            }).ToList();
            await (await _admin.PostAsJsonAsync("/api/purchases", new { supplierId, date = p.GetProperty("date").GetString(), supplierInvoiceNo = p.GetProperty("key").GetString(), lines }))
                .ReadAsync<PurchaseDto>(HttpStatusCode.Created);
        }

        var clients = new Dictionary<string, int>();
        foreach (var i in Spec.GetProperty("invoices").EnumerateArray())
        {
            var name = i.GetProperty("client").GetString()!;
            if (!clients.TryGetValue(name, out var clientId))
            {
                clients[name] = clientId = (await Scenario.ClientAsync(_admin, name)).Id;
            }

            var lines = i.GetProperty("lines").EnumerateArray().Select(l => new
            {
                itemId = l.TryGetProperty("item", out _) ? _dishwasher.Id : (int?)null,
                description = l.GetProperty("description").GetString(),
                quantity = Dec(l, "qty"),
                unitPrice = Dec(l, "unitPrice"),
                taxCategory = l.GetProperty("tax").GetString(),
            }).ToList();
            var invoice = (await (await _admin.PostAsJsonAsync("/api/invoices", new { clientId, date = i.GetProperty("date").GetString(), lines }))
                .ReadAsync<InvoiceResult>(HttpStatusCode.Created)).Invoice;
            if (i.TryGetProperty("void", out var reason))
            {
                invoice = await (await _admin.PostAsJsonAsync($"/api/invoices/{invoice.Id}/void", new { reason = reason.GetString() })).ReadAsync<InvoiceDto>();
            }

            _invoices[i.GetProperty("key").GetString()!] = invoice;
        }

        foreach (var c in Spec.GetProperty("creditNotes").EnumerateArray())
        {
            var invoice = _invoices[c.GetProperty("invoice").GetString()!];
            var lines = c.GetProperty("lines").EnumerateArray().Select(l => new
            {
                invoiceLineId = invoice.Lines.Single(x => x.LineNo == l.GetProperty("line").GetInt32()).Id,
                quantity = Dec(l, "qty"),
            }).ToList();
            await (await _admin.PostAsJsonAsync("/api/credit-notes", new
            {
                invoiceId = invoice.Id,
                date = c.GetProperty("date").GetString(),
                reason = c.GetProperty("reason").GetString(),
                returnToStock = c.GetProperty("returnToStock").GetBoolean(),
                lines,
            })).ReadAsync<CreditNoteDto>(HttpStatusCode.Created);
        }
    }

    public ValueTask DisposeAsync() => Api.DisposeAsync();

    public static decimal Dec(JsonElement e, string name) => Dec(e.GetProperty(name));

    public static decimal Dec(JsonElement e) => decimal.Parse(e.GetString()!, CultureInfo.InvariantCulture);

    private static JsonElement LoadSpec()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "spec", "report-scenario.json"));
        return JsonDocument.Parse(stream).RootElement.Clone();
    }
}

/// <summary>Checks every report figure against the hand calculations in spec/report-scenario.json.</summary>
public class ReportsTests(ReportScenarioFixture fixture) : IClassFixture<ReportScenarioFixture>
{
    private static readonly JsonElement Spec = ReportScenarioFixture.Spec;
    private readonly ApiFactory api = fixture.Api;
    private readonly HttpClient _admin = fixture.Admin;
    private readonly ItemDto _dishwasher = fixture.Dishwasher;
    private readonly Dictionary<string, InvoiceDto> _invoices = fixture.Invoices;

    private static string Query => $"from={Spec.GetProperty("period").GetProperty("from").GetString()}&to={Spec.GetProperty("period").GetProperty("to").GetString()}";

    private static decimal Dec(JsonElement e, string name) => ReportScenarioFixture.Dec(e, name);

    private static decimal Dec(JsonElement e) => ReportScenarioFixture.Dec(e);

    [Fact]
    public async Task Sales_report_matches_hand_calculation()
    {
        var expected = Spec.GetProperty("expected").GetProperty("sales");
        var report = await (await _admin.GetAsync($"/api/reports/sales?{Query}")).ReadAsync<SalesReportDto>();
        var s = report.Summary;
        s.InvoiceCount.ShouldBe(expected.GetProperty("invoiceCount").GetInt32());
        s.InvoicesNet.ShouldBe(Dec(expected, "invoicesNet"));
        s.InvoicesVat.ShouldBe(Dec(expected, "invoicesVat"));
        s.InvoicesTotal.ShouldBe(Dec(expected, "invoicesTotal"));
        s.CreditNoteCount.ShouldBe(expected.GetProperty("creditNoteCount").GetInt32());
        s.CreditNotesNet.ShouldBe(Dec(expected, "creditNotesNet"));
        s.CreditNotesVat.ShouldBe(Dec(expected, "creditNotesVat"));
        s.CreditNotesTotal.ShouldBe(Dec(expected, "creditNotesTotal"));
        s.NetSales.ShouldBe(Dec(expected, "netSales"));
        s.NetVat.ShouldBe(Dec(expected, "netVat"));
        s.NetTotal.ShouldBe(Dec(expected, "netTotal"));
        report.Rows.Sum(r => r.Total).ShouldBe(s.NetTotal);
        report.Rows.ShouldNotContain(r => r.Number == _invoices["I3"].Number); // void excluded

        // The cashier breakdown nets each cashier's credit notes, so it adds up to the summary.
        report.ByCashier.Sum(c => c.Net).ShouldBe(s.NetSales);
        report.ByCashier.Sum(c => c.Total).ShouldBe(s.NetTotal);
        report.ByCashier.Sum(c => c.InvoiceCount).ShouldBe(s.InvoiceCount);
    }

    [Fact]
    public async Task Purchases_report_matches_hand_calculation()
    {
        var expected = Spec.GetProperty("expected").GetProperty("purchases");
        var report = await (await _admin.GetAsync($"/api/reports/purchases?{Query}")).ReadAsync<PurchasesReportDto>();
        report.Count.ShouldBe(expected.GetProperty("count").GetInt32());
        report.Net.ShouldBe(Dec(expected, "net"));
        report.Vat.ShouldBe(Dec(expected, "vat"));
        report.Total.ShouldBe(Dec(expected, "total"));
        report.BySupplier.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Profit_and_loss_matches_hand_calculation()
    {
        var expected = Spec.GetProperty("expected").GetProperty("profitLoss");
        var pl = await (await _admin.GetAsync($"/api/reports/profit-loss?{Query}")).ReadAsync<ProfitLossDto>();
        pl.Sales.ShouldBe(Dec(expected, "sales"));
        pl.CreditNotes.ShouldBe(Dec(expected, "creditNotes"));
        pl.NetSales.ShouldBe(Dec(expected, "netSales"));
        pl.InventoryPurchases.ShouldBe(Dec(expected, "inventoryPurchases"));
        foreach (var e in expected.GetProperty("expenses").EnumerateObject())
        {
            pl.Expenses.Single(x => x.Category == e.Name).Amount.ShouldBe(Dec(e.Value));
        }

        pl.TotalPurchases.ShouldBe(Dec(expected, "totalPurchases"));
        pl.NetProfit.ShouldBe(Dec(expected, "netProfit"));
        pl.Monthly.Single().Profit.ShouldBe(pl.NetProfit);
    }

    [Fact]
    public async Task Vat_report_matches_hand_calculation()
    {
        var expected = Spec.GetProperty("expected").GetProperty("vat");
        var vat = await (await _admin.GetAsync($"/api/reports/vat?{Query}")).ReadAsync<VatReportDto>();
        foreach (var box in expected.GetProperty("boxes").EnumerateObject())
        {
            var actual = vat.Boxes.Single(b => b.Box == box.Name);
            var values = box.Value.EnumerateArray().Select(Dec).ToList();
            actual.Amount.ShouldBe(values[0], $"box {box.Name} amount");
            actual.Vat.ShouldBe(values[1], $"box {box.Name} VAT");
        }

        vat.OutputVat.ShouldBe(Dec(expected, "outputVat"));
        vat.InputVat.ShouldBe(Dec(expected, "inputVat"));
        vat.NetVatPayable.ShouldBe(Dec(expected, "netVatPayable"));
    }

    [Fact]
    public async Task Stock_and_average_cost_follow_purchases_sales_voids_and_returns()
    {
        var expected = Spec.GetProperty("expected");
        var item = await (await _admin.GetAsync($"/api/items/{_dishwasher.Id}")).ReadAsync<ItemDto>();
        item.StockQty.ShouldBe(Dec(expected, "dishwasherStock"));
        item.AvgCost.ShouldBe(Dec(expected, "dishwasherAvgCost"));
    }

    [Fact]
    public async Task Cashier_sees_only_own_sales_and_no_admin_reports()
    {
        var (cashier, cashierId, _) = await api.NewCashierAsync("Report Cashier");
        var client = await Scenario.ClientAsync(cashier, "Walk-in");
        await (await cashier.PostAsJsonAsync("/api/invoices", new
        {
            clientId = client.Id,
            lines = new[] { new { description = "Counter sale", quantity = 1m, unitPrice = 200m, taxCategory = "Standard" } },
        })).ReadAsync<InvoiceResult>(HttpStatusCode.Created);

        // Even when asking for everything (or for someone else), a cashier gets only their own sales.
        var own = await (await cashier.GetAsync($"/api/reports/sales?from=2026-01-01&cashierId={Guid.NewGuid()}")).ReadAsync<SalesReportDto>();
        own.Summary.InvoiceCount.ShouldBe(1);
        own.Summary.InvoicesTotal.ShouldBe(210m);
        own.ByCashier.Single().CashierId.ShouldBe(cashierId);

        foreach (var url in new[] { "/api/reports/purchases", "/api/reports/profit-loss", "/api/reports/vat", "/api/dashboard/admin", "/api/purchases" })
        {
            (await cashier.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden, url);
        }

        var dash = await (await cashier.GetAsync("/api/dashboard/cashier")).ReadAsync<CashierDashboardDto>();
        dash.ShiftInvoiceCount.ShouldBe(1);
        dash.ShiftSalesTotal.ShouldBe(210m);
        dash.RecentInvoices.Single().ClientName.ShouldBe("Walk-in");
    }

    [Fact]
    public async Task Admin_dashboard_summarises_the_business()
    {
        var dash = await (await _admin.GetAsync($"/api/dashboard/admin?{Query}")).ReadAsync<AdminDashboardDto>();
        var pl = Spec.GetProperty("expected").GetProperty("profitLoss");
        dash.Sales.ShouldBe(Dec(pl, "netSales"));
        dash.Purchases.ShouldBe(Dec(pl, "totalPurchases"));
        dash.NetProfit.ShouldBe(Dec(pl, "netProfit"));
        dash.Monthly.Count.ShouldBe(12);
        dash.OpenInvoiceCount.ShouldBeGreaterThanOrEqualTo(2); // I1 and I2 are unpaid
        dash.LowStockItems.ShouldAllBe(i => i.StockQty <= i.ReorderLevel);
    }

    [Fact]
    public async Task Report_period_is_validated()
    {
        (await _admin.GetAsync("/api/reports/sales?from=2026-09-30&to=2026-09-01")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _admin.GetAsync("/api/reports/vat?from=2010-01-01&to=2026-09-01")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Purchase_edit_and_delete_reverse_stock()
    {
        var supplier = await (await _admin.PostAsJsonAsync("/api/suppliers", new { name = "Edit Supplier" })).ReadAsync<SupplierDto>(HttpStatusCode.Created);
        var item = await Scenario.DishwasherAsync(_admin);
        object Line(decimal qty) => new { itemId = item.Id, description = "Unit", quantity = qty, unitPrice = 100m, taxCategory = "Standard" };

        var p = await (await _admin.PostAsJsonAsync("/api/purchases", new { supplierId = supplier.Id, date = "2026-09-01", lines = new[] { Line(5) } }))
            .ReadAsync<PurchaseDto>(HttpStatusCode.Created);
        (await (await _admin.GetAsync($"/api/items/{item.Id}")).ReadAsync<ItemDto>()).StockQty.ShouldBe(5m);

        await (await _admin.PutAsJsonAsync($"/api/purchases/{p.Id}", new { supplierId = supplier.Id, date = "2026-09-01", lines = new[] { Line(8) } })).ReadAsync<PurchaseDto>();
        (await (await _admin.GetAsync($"/api/items/{item.Id}")).ReadAsync<ItemDto>()).StockQty.ShouldBe(8m);

        (await _admin.DeleteAsync($"/api/purchases/{p.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await (await _admin.GetAsync($"/api/items/{item.Id}")).ReadAsync<ItemDto>()).StockQty.ShouldBe(0m);

        var future = await _admin.PostAsJsonAsync("/api/purchases", new { supplierId = supplier.Id, date = "2099-01-01", lines = new[] { Line(1) } });
        future.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

}
