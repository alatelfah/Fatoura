using System.Net;
using System.Net.Http.Json;
using Fatoura.Api.Contacts;
using Fatoura.Api.Documents;
using Fatoura.Api.Items;
using Fatoura.Api.Purchases;
using Fatoura.Api.Reports;
using Fatoura.Api.Settings;
using Fatoura.IntegrationTests.Infrastructure;

namespace Fatoura.IntegrationTests;

public class CurrencyTests(ApiFactory api) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private HttpClient _admin = null!;

    public async ValueTask InitializeAsync()
    {
        _admin = await api.AdminAsync();
        await Scenario.ConfigureCompanyAsync(_admin);
        await SetRateAsync("USD", 3.6725m);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<CurrencyDto> SetRateAsync(string code, decimal rate, bool isActive = true) =>
        await (await _admin.PutAsJsonAsync($"/api/settings/currencies/{code}", new { name = $"{code} name", rateToAed = rate, isActive }))
            .ReadAsync<CurrencyDto>();

    private static object[] Lines(decimal qty = 10, decimal price = 99.99m) =>
        [new { description = "Consulting", quantity = qty, unitPrice = price, taxCategory = "Standard" }];

    [Fact]
    public async Task Admin_manages_currencies_and_staff_can_read_them()
    {
        var (cashier, _, _) = await api.NewCashierAsync();
        (await (await cashier.GetAsync("/api/settings/currencies")).ReadAsync<List<CurrencyDto>>()).ShouldContain(c => c.Code == "USD" && c.RateToAed == 3.6725m);
        (await cashier.PutAsJsonAsync("/api/settings/currencies/EUR", new { name = "Euro", rateToAed = 4m })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        foreach (var (code, rate, field) in new[] { ("AED", 1m, "code"), ("EURO", 4m, "code"), ("GBP", 0m, "rateToAed"), ("GBP", 4.1234567m, "rateToAed") })
        {
            var r = await _admin.PutAsJsonAsync($"/api/settings/currencies/{code}", new { name = "X", rateToAed = rate });
            (await r.ReadJsonAsync(HttpStatusCode.BadRequest)).GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue($"{code} {rate}");
        }

        (await SetRateAsync("gbp", 4.95m)).Code.ShouldBe("GBP");
        (await _admin.DeleteAsync("/api/settings/currencies/GBP")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await (await _admin.GetAsync("/api/settings/currencies")).ReadAsync<List<CurrencyDto>>()).ShouldNotContain(c => c.Code == "GBP");
    }

    [Fact]
    public async Task Usd_invoice_keeps_its_rate_and_reports_in_aed()
    {
        var client = await Scenario.ClientAsync(_admin, "USD Client");
        var invoice = (await (await _admin.PostAsJsonAsync("/api/invoices", new { clientId = client.Id, lines = Lines(), currency = "USD" }))
            .ReadAsync<InvoiceResult>(HttpStatusCode.Created)).Invoice;
        (invoice.SubTotal, invoice.VatTotal, invoice.Total).ShouldBe((999.90m, 50.00m, 1049.90m));
        invoice.Currency.ShouldBe(new DocumentCurrencyDto("USD", 3.6725m, 3672.13m, 183.63m, 3855.76m));
        invoice.Balance.ShouldBe(1049.90m); // payments and balances are in the invoice's currency

        // A later rate change does not touch the invoice; its credit note uses the invoice's rate.
        await SetRateAsync("USD", 3.7m);
        var note = await (await _admin.PostAsJsonAsync("/api/credit-notes", new
        {
            invoiceId = invoice.Id, reason = "Return", returnToStock = false, lines = new[] { new { invoiceLineId = invoice.Lines[0].Id, quantity = 10m } },
        })).ReadAsync<CreditNoteDto>(HttpStatusCode.Created);
        note.Currency.ShouldBe(invoice.Currency);
        (await (await _admin.GetAsync($"/api/invoices/{invoice.Id}")).ReadAsync<InvoiceDto>()).Currency.ExchangeRate.ShouldBe(3.6725m);
        await SetRateAsync("USD", 3.6725m);

        (await _admin.GetAsync($"/api/invoices/{invoice.Id}/pdf")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _admin.GetAsync($"/api/credit-notes/{note.Id}/pdf")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var day = invoice.Date.ToString("yyyy-MM-dd");
        var sales = await (await _admin.GetAsync($"/api/reports/sales?from={day}&to={day}&clientId={client.Id}")).ReadAsync<SalesReportDto>();
        sales.Rows.ShouldContain(r => r.Number == invoice.Number && r.Currency == "USD" && r.Net == 3672.13m && r.Vat == 183.63m && r.Total == 3855.76m);
        sales.Summary.NetTotal.ShouldBe(0m); // fully credited at the same rate
    }

    [Fact]
    public async Task Converting_a_quotation_uses_todays_rate_and_an_explicit_rate_wins()
    {
        var client = await Scenario.ClientAsync(_admin, "EUR Client");
        await SetRateAsync("EUR", 4.012345m);
        var quote = await (await _admin.PostAsJsonAsync("/api/quotations", new { clientId = client.Id, lines = Lines(1, 100m), currency = "EUR" }))
            .ReadAsync<QuotationDto>(HttpStatusCode.Created);
        quote.Currency.ExchangeRate.ShouldBe(4.012345m);

        await SetRateAsync("EUR", 4.1m);
        var invoice = (await (await _admin.PostAsync($"/api/quotations/{quote.Id}/convert", null)).ReadAsync<InvoiceResult>(HttpStatusCode.Created)).Invoice;
        invoice.Currency.ShouldBe(new DocumentCurrencyDto("EUR", 4.1m, 410m, 20.5m, 430.5m));

        var manual = (await (await _admin.PostAsJsonAsync("/api/invoices", new { clientId = client.Id, lines = Lines(1, 100m), currency = "EUR", exchangeRate = 4.25m }))
            .ReadAsync<InvoiceResult>(HttpStatusCode.Created)).Invoice;
        manual.Currency.ExchangeRate.ShouldBe(4.25m);

        // An admin edit that omits the currency keeps the invoice's currency and rate.
        var edited = (await (await _admin.PutAsJsonAsync($"/api/invoices/{manual.Id}", new { clientId = client.Id, date = manual.Date, lines = Lines(2, 100m) }))
            .ReadAsync<InvoiceResult>()).Invoice;
        edited.Currency.ShouldBe(new DocumentCurrencyDto("EUR", 4.25m, 850m, 42.5m, 892.5m));
    }

    [Fact]
    public async Task Unknown_or_inactive_currencies_and_bad_rates_are_rejected()
    {
        var client = await Scenario.ClientAsync(_admin, "Bad Currency");
        await SetRateAsync("JPY", 0.0245m, isActive: false);
        foreach (var (body, field) in new (object, string)[]
        {
            (new { clientId = client.Id, lines = Lines(), currency = "XYZ" }, "currency"),
            (new { clientId = client.Id, lines = Lines(), currency = "JPY" }, "currency"),
            (new { clientId = client.Id, lines = Lines(), currency = "USD", exchangeRate = 0m }, "exchangeRate"),
            (new { clientId = client.Id, lines = Lines(), currency = "USD", exchangeRate = 3.1234567m }, "exchangeRate"),
        })
        {
            var r = await _admin.PostAsJsonAsync("/api/invoices", body);
            (await r.ReadJsonAsync(HttpStatusCode.BadRequest)).GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue(field);
        }

        // AED ignores any rate sent with it.
        var aed = (await (await _admin.PostAsJsonAsync("/api/invoices", new { clientId = client.Id, lines = Lines(), currency = "AED", exchangeRate = 2m }))
            .ReadAsync<InvoiceResult>(HttpStatusCode.Created)).Invoice;
        aed.Currency.ShouldBe(new DocumentCurrencyDto("AED", 1m, aed.SubTotal, aed.VatTotal, aed.Total));
    }

    [Fact]
    public async Task Usd_purchase_costs_stock_and_input_vat_in_aed()
    {
        var supplier = await (await _admin.PostAsJsonAsync("/api/suppliers", new { name = "US Supplier" })).ReadAsync<SupplierDto>(HttpStatusCode.Created);
        var item = await Scenario.DishwasherAsync(_admin);
        var date = new DateOnly(2025, 3, 10);
        var purchase = await (await _admin.PostAsJsonAsync("/api/purchases", new
        {
            supplierId = supplier.Id,
            date,
            currency = "USD",
            lines = new[] { new { itemId = item.Id, description = "Dishwasher", quantity = 4m, unitPrice = 400m, taxCategory = "Standard" } },
        })).ReadAsync<PurchaseDto>(HttpStatusCode.Created);
        purchase.Currency.ShouldBe(new DocumentCurrencyDto("USD", 3.6725m, 5876m, 293.8m, 6169.8m));

        var stocked = await (await _admin.GetAsync($"/api/items/{item.Id}")).ReadAsync<ItemDto>();
        stocked.AvgCost.ShouldBe(1469m); // 400 USD × 3.6725

        var vat = await (await _admin.GetAsync("/api/reports/vat?from=2025-03-01&to=2025-03-31")).ReadAsync<VatReportDto>();
        vat.InputVat.ShouldBe(293.8m);
        var pl = await (await _admin.GetAsync("/api/reports/profit-loss?from=2025-03-01&to=2025-03-31")).ReadAsync<ProfitLossDto>();
        pl.InventoryPurchases.ShouldBe(5876m);

        (await _admin.DeleteAsync("/api/settings/currencies/USD")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await (await _admin.GetAsync("/api/settings/currencies")).ReadAsync<List<CurrencyDto>>()).Single(c => c.Code == "USD").IsActive.ShouldBeFalse();
        await SetRateAsync("USD", 3.6725m);
    }
}
