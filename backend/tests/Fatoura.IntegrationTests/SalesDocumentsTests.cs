using System.Net;
using System.Net.Http.Json;
using Fatoura.Api.Data.Entities;
using Fatoura.Api.Documents;
using Fatoura.Api.Infrastructure;
using Fatoura.Api.Items;
using Fatoura.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fatoura.IntegrationTests;

public class SalesDocumentsTests(ApiFactory api) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private HttpClient _admin = null!;

    public async ValueTask InitializeAsync()
    {
        _admin = await api.AdminAsync();
        await Scenario.ConfigureCompanyAsync(_admin);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Reference_invoice_totals_match_the_sample_pdf()
    {
        var client = await Scenario.ClientAsync(_admin);
        var dishwasher = await Scenario.DishwasherAsync(_admin, openingStock: 25);
        var install = await Scenario.InstallationAsync(_admin);

        var result = await (await _admin.PostAsJsonAsync("/api/invoices", new
        {
            clientId = client.Id,
            lines = Scenario.ReferenceLines(dishwasher, install),
        })).ReadAsync<InvoiceResult>(HttpStatusCode.Created);

        var inv = result.Invoice;
        inv.Lines.Select(l => (l.Net, l.Vat, l.Total)).ShouldBe([(41000m, 2050m, 43050m), (5000m, 250m, 5250m)]);
        inv.SubTotal.ShouldBe(46000m);
        inv.VatTotal.ShouldBe(2300m);
        inv.Total.ShouldBe(48300m);
        inv.Number.ShouldMatch(@"^INV/[A-Z]{3}/\d{2}\d{4}$");
        inv.Client.Trn.ShouldBe("105325228200003");
        inv.Company.Trn.ShouldBe("105386581000003");
        inv.Terms.PaymentTerms.ShouldStartWith("30% payment");
        inv.Balance.ShouldBe(48300m);
        result.Warnings.ShouldBeEmpty();

        var item = await (await _admin.GetAsync($"/api/items/{dishwasher.Id}")).ReadAsync<ItemDto>();
        item.StockQty.ShouldBe(5m);
    }

    [Fact]
    public async Task Concurrent_issues_get_unique_gap_free_numbers()
    {
        var client = await Scenario.ClientAsync(_admin, "Concurrency LLC");
        var (cashier, _, _) = await api.NewCashierAsync();
        var before = await api.WithDbAsync(db => db.Invoices.CountAsync());

        var requests = Enumerable.Range(0, 50).Select(i => (i % 2 == 0 ? _admin : cashier).PostAsJsonAsync("/api/invoices", new
        {
            clientId = client.Id,
            lines = new[] { new { description = $"Service {i}", quantity = 1m, unitPrice = 100m, taxCategory = "Standard" } },
        }));
        var responses = await Task.WhenAll(requests);
        foreach (var r in responses)
        {
            r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        }

        var numbers = await api.WithDbAsync(db => db.Invoices.Select(i => i.Number).ToListAsync());
        numbers.Count.ShouldBe(before + 50);
        numbers.Distinct().Count().ShouldBe(numbers.Count);
        var sequence = numbers.Select(n => int.Parse(n[^4..], System.Globalization.CultureInfo.InvariantCulture)).Order().ToList();
        sequence.ShouldBe(Enumerable.Range(1, sequence.Count).ToList());
    }

    [Fact]
    public async Task Short_stock_warns_by_default_and_blocks_when_configured_without_burning_a_number()
    {
        var client = await Scenario.ClientAsync(_admin, "Stock LLC");
        var item = await Scenario.DishwasherAsync(_admin, openingStock: 1);
        var line = new { itemId = item.Id, description = "Dishwasher", quantity = 3m, unitPrice = 2050m, taxCategory = "Standard" };

        var warned = await (await _admin.PostAsJsonAsync("/api/invoices", new { clientId = client.Id, lines = new[] { line } }))
            .ReadAsync<InvoiceResult>(HttpStatusCode.Created);
        warned.Warnings.Single().StockAfter.ShouldBe(-2m);

        await Scenario.ConfigureCompanyAsync(_admin, allowNegativeStock: false);
        try
        {
            var blocked = await _admin.PostAsJsonAsync("/api/invoices", new { clientId = client.Id, lines = new[] { line } });
            (await blocked.ReadJsonAsync(HttpStatusCode.Conflict)).GetProperty("code").GetString().ShouldBe("insufficient_stock");

            // The rolled-back invoice did not consume a number.
            var next = await (await _admin.PostAsJsonAsync("/api/invoices", new
            {
                clientId = client.Id,
                lines = new[] { new { description = "Labour", quantity = 1m, unitPrice = 10m, taxCategory = "Standard" } },
            })).ReadAsync<InvoiceResult>(HttpStatusCode.Created);
            var previous = int.Parse(warned.Invoice.Number[^4..], System.Globalization.CultureInfo.InvariantCulture);
            var latestBefore = await api.WithDbAsync(db => db.Invoices.Where(i => i.Id != next.Invoice.Id).Select(i => i.Number).ToListAsync());
            int.Parse(next.Invoice.Number[^4..], System.Globalization.CultureInfo.InvariantCulture)
                .ShouldBe(latestBefore.Max(n => int.Parse(n[^4..], System.Globalization.CultureInfo.InvariantCulture)) + 1);
            previous.ShouldBeGreaterThan(0);
        }
        finally
        {
            await Scenario.ConfigureCompanyAsync(_admin);
        }
    }

    [Fact]
    public async Task Quotation_converts_once_into_an_invoice_with_the_same_lines_and_terms()
    {
        var client = await Scenario.ClientAsync(_admin, "Quote Client");
        var dishwasher = await Scenario.DishwasherAsync(_admin, openingStock: 40);
        var install = await Scenario.InstallationAsync(_admin);
        var (cashier, _, _) = await api.NewCashierAsync();

        var quote = await (await cashier.PostAsJsonAsync("/api/quotations", new
        {
            clientId = client.Id,
            lines = Scenario.ReferenceLines(dishwasher, install),
            terms = new { completionOfWork = "Within 2 weeks" },
        })).ReadAsync<QuotationDto>(HttpStatusCode.Created);
        quote.Number.ShouldStartWith("QUO/");
        quote.Total.ShouldBe(48300m);
        quote.Status.ShouldBe(QuotationStatus.Draft);
        quote.Terms.CompletionOfWork.ShouldBe("Within 2 weeks");
        quote.Terms.PaymentTerms.ShouldStartWith("30% payment"); // default from settings
        quote.ValidUntil.ShouldBe(quote.Date.AddDays(30));

        (await cashier.PutAsJsonAsync($"/api/quotations/{quote.Id}/status", new { status = "Accepted" })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var converted = await (await cashier.PostAsync($"/api/quotations/{quote.Id}/convert", null)).ReadAsync<InvoiceResult>(HttpStatusCode.Created);
        converted.Invoice.QuotationNumber.ShouldBe(quote.Number);
        converted.Invoice.Total.ShouldBe(48300m);
        converted.Invoice.Terms.CompletionOfWork.ShouldBe("Within 2 weeks");
        converted.Invoice.Lines.Count.ShouldBe(2);

        var again = await cashier.PostAsync($"/api/quotations/{quote.Id}/convert", null);
        (await again.ReadJsonAsync(HttpStatusCode.Conflict)).GetProperty("code").GetString().ShouldBe("already_converted");

        var reloaded = await (await cashier.GetAsync($"/api/quotations/{quote.Id}")).ReadAsync<QuotationDto>();
        reloaded.Status.ShouldBe(QuotationStatus.Converted);
        reloaded.ConvertedInvoiceNumber.ShouldBe(converted.Invoice.Number);
        (await cashier.PutAsJsonAsync($"/api/quotations/{quote.Id}", new { clientId = client.Id, lines = Scenario.ReferenceLines(dishwasher, install) }))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Cashier_cannot_edit_another_users_quotation()
    {
        var client = await Scenario.ClientAsync(_admin, "Owner Test");
        var quote = await (await _admin.PostAsJsonAsync("/api/quotations", new
        {
            clientId = client.Id,
            lines = new[] { new { description = "Consulting", quantity = 1m, unitPrice = 500m, taxCategory = "Standard" } },
        })).ReadAsync<QuotationDto>(HttpStatusCode.Created);

        var (cashier, _, _) = await api.NewCashierAsync();
        (await cashier.GetAsync($"/api/quotations/{quote.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await cashier.PutAsJsonAsync($"/api/quotations/{quote.Id}/status", new { status = "Sent" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await cashier.DeleteAsync($"/api/quotations/{quote.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Cashier_issues_with_todays_date_and_cannot_edit_or_void()
    {
        var client = await Scenario.ClientAsync(_admin, "Cashier Client");
        var (cashier, _, _) = await api.NewCashierAsync();
        var result = await (await cashier.PostAsJsonAsync("/api/invoices", new
        {
            clientId = client.Id,
            date = "2020-01-01",
            lines = new[] { new { description = "Repair", quantity = 1m, unitPrice = 100m, taxCategory = "Standard" } },
            payment = new { amount = 105m, method = "Cash" },
        })).ReadAsync<InvoiceResult>(HttpStatusCode.Created);

        var today = api.Services.GetRequiredService<BusinessClock>().Today;
        result.Invoice.Date.ShouldBe(today);
        result.Invoice.Balance.ShouldBe(0m);
        result.Invoice.Payments.Single().Method.ShouldBe(PaymentMethod.Cash);

        (await cashier.PostAsJsonAsync($"/api/invoices/{result.Invoice.Id}/void", new { reason = "x" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await cashier.PutAsJsonAsync($"/api/invoices/{result.Invoice.Id}", new { clientId = client.Id, date = today, lines = Array.Empty<object>() }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_cannot_post_date_an_invoice()
    {
        var client = await Scenario.ClientAsync(_admin, "Future");
        var r = await _admin.PostAsJsonAsync("/api/invoices", new
        {
            clientId = client.Id,
            date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            lines = new[] { new { description = "Later", quantity = 1m, unitPrice = 1m, taxCategory = "Standard" } },
        });
        r.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Credit_notes_reduce_the_balance_return_stock_and_cannot_exceed_the_invoice()
    {
        var client = await Scenario.ClientAsync(_admin, "Return Client");
        var dishwasher = await Scenario.DishwasherAsync(_admin, openingStock: 20);
        var install = await Scenario.InstallationAsync(_admin);
        var invoice = (await (await _admin.PostAsJsonAsync("/api/invoices", new { clientId = client.Id, lines = Scenario.ReferenceLines(dishwasher, install) }))
            .ReadAsync<InvoiceResult>(HttpStatusCode.Created)).Invoice;
        var dishLine = invoice.Lines.Single(l => l.ItemId == dishwasher.Id);

        var (cashier, _, _) = await api.NewCashierAsync();
        var over = await cashier.PostAsJsonAsync("/api/credit-notes", new
        {
            invoiceId = invoice.Id,
            reason = "Too many",
            returnToStock = true,
            lines = new[] { new { invoiceLineId = dishLine.Id, quantity = 21m } },
        });
        over.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var note = await (await cashier.PostAsJsonAsync("/api/credit-notes", new
        {
            invoiceId = invoice.Id,
            reason = "2 units returned damaged",
            returnToStock = true,
            lines = new[] { new { invoiceLineId = dishLine.Id, quantity = 2m } },
        })).ReadAsync<CreditNoteDto>(HttpStatusCode.Created);
        note.Number.ShouldStartWith("CN/");
        note.InvoiceNumber.ShouldBe(invoice.Number);
        note.SubTotal.ShouldBe(4100m);
        note.VatTotal.ShouldBe(205m);
        note.Total.ShouldBe(4305m);

        var after = await (await _admin.GetAsync($"/api/invoices/{invoice.Id}")).ReadAsync<InvoiceDto>();
        after.CreditedTotal.ShouldBe(4305m);
        after.Balance.ShouldBe(48300m - 4305m);
        after.Lines.Single(l => l.Id == dishLine.Id).CreditedQuantity.ShouldBe(2m);

        var stock = await (await _admin.GetAsync($"/api/items/{dishwasher.Id}")).ReadAsync<ItemDto>();
        stock.StockQty.ShouldBe(2m); // 20 - 20 sold + 2 returned

        // Remaining 18 can be credited, but not 19.
        var tooMuch = await cashier.PostAsJsonAsync("/api/credit-notes", new
        {
            invoiceId = invoice.Id, reason = "More", returnToStock = false, lines = new[] { new { invoiceLineId = dishLine.Id, quantity = 19m } },
        });
        tooMuch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // An invoice with credit notes can be neither edited nor voided.
        (await _admin.PostAsJsonAsync($"/api/invoices/{invoice.Id}/void", new { reason = "Mistake" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Void_returns_stock_keeps_the_number_and_zeroes_the_balance()
    {
        var client = await Scenario.ClientAsync(_admin, "Void Client");
        var dishwasher = await Scenario.DishwasherAsync(_admin, openingStock: 10);
        var invoice = (await (await _admin.PostAsJsonAsync("/api/invoices", new
        {
            clientId = client.Id,
            lines = new[] { new { itemId = dishwasher.Id, description = "Dishwasher", quantity = 4m, unitPrice = 2050m, taxCategory = "Standard" } },
        })).ReadAsync<InvoiceResult>(HttpStatusCode.Created)).Invoice;

        (await _admin.PostAsJsonAsync($"/api/invoices/{invoice.Id}/void", new { reason = "" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var voided = await (await _admin.PostAsJsonAsync($"/api/invoices/{invoice.Id}/void", new { reason = "Issued to wrong client" })).ReadAsync<InvoiceDto>();
        voided.Status.ShouldBe(InvoiceStatus.Void);
        voided.Number.ShouldBe(invoice.Number);
        voided.Balance.ShouldBe(0m);

        (await (await _admin.GetAsync($"/api/items/{dishwasher.Id}")).ReadAsync<ItemDto>()).StockQty.ShouldBe(10m);
        (await _admin.PostAsJsonAsync($"/api/invoices/{invoice.Id}/void", new { reason = "Again" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await _admin.PostAsJsonAsync($"/api/invoices/{invoice.Id}/payments", new { amount = 1m, method = "Cash" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var audit = await api.WithDbAsync(db => db.AuditLogs.Where(a => a.EntityType == "Invoice" && a.EntityId == invoice.Id.ToString()).ToListAsync());
        audit.ShouldContain(a => a.Changes.Contains("Issued to wrong client"));
    }

    [Fact]
    public async Task Admin_edit_reapplies_stock_and_keeps_the_number()
    {
        var client = await Scenario.ClientAsync(_admin, "Edit Client");
        var dishwasher = await Scenario.DishwasherAsync(_admin, openingStock: 10);
        var invoice = (await (await _admin.PostAsJsonAsync("/api/invoices", new
        {
            clientId = client.Id,
            lines = new[] { new { itemId = dishwasher.Id, description = "Dishwasher", quantity = 4m, unitPrice = 2050m, taxCategory = "Standard" } },
        })).ReadAsync<InvoiceResult>(HttpStatusCode.Created)).Invoice;

        var edited = await (await _admin.PutAsJsonAsync($"/api/invoices/{invoice.Id}", new
        {
            clientId = client.Id,
            date = invoice.Date,
            lines = new[]
            {
                new { itemId = (int?)dishwasher.Id, description = "Dishwasher", quantity = 6m, unitPrice = 2000m, taxCategory = "Standard" },
                new { itemId = (int?)null, description = "Delivery", quantity = 1m, unitPrice = 100m, taxCategory = "ZeroRated" },
            },
        })).ReadAsync<InvoiceResult>();
        edited.Invoice.Number.ShouldBe(invoice.Number);
        edited.Invoice.SubTotal.ShouldBe(12100m);
        edited.Invoice.VatTotal.ShouldBe(600m);
        edited.Invoice.Total.ShouldBe(12700m);
        (await (await _admin.GetAsync($"/api/items/{dishwasher.Id}")).ReadAsync<ItemDto>()).StockQty.ShouldBe(4m);
    }

    [Fact]
    public async Task Payments_cannot_exceed_the_balance_and_unpaid_filter_works()
    {
        var client = await Scenario.ClientAsync(_admin, "Payer");
        var invoice = (await (await _admin.PostAsJsonAsync("/api/invoices", new
        {
            clientId = client.Id,
            lines = new[] { new { description = "Work", quantity = 1m, unitPrice = 1000m, taxCategory = "Standard" } },
        })).ReadAsync<InvoiceResult>(HttpStatusCode.Created)).Invoice;

        (await _admin.PostAsJsonAsync($"/api/invoices/{invoice.Id}/payments", new { amount = 1050.01m, method = "Card" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var partial = await (await _admin.PostAsJsonAsync($"/api/invoices/{invoice.Id}/payments", new { amount = 50m, method = "BankTransfer", reference = "TT-1" }))
            .ReadAsync<InvoiceDto>();
        partial.Balance.ShouldBe(1000m);

        var unpaid = await (await _admin.GetAsync($"/api/invoices?unpaidOnly=true&clientId={client.Id}")).ReadAsync<PagedResult<InvoiceSummaryDto>>();
        unpaid.Items.Single().Balance.ShouldBe(1000m);

        await _admin.PostAsJsonAsync($"/api/invoices/{invoice.Id}/payments", new { amount = 1000m, method = "Cash" });
        unpaid = await (await _admin.GetAsync($"/api/invoices?unpaidOnly=true&clientId={client.Id}")).ReadAsync<PagedResult<InvoiceSummaryDto>>();
        unpaid.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Issuing_requires_complete_company_settings()
    {
        await using var fresh = new ApiFactory();
        await fresh.InitializeAsync();
        var admin = await fresh.AdminAsync();
        var client = await Scenario.ClientAsync(admin);
        var r = await admin.PostAsJsonAsync("/api/invoices", new
        {
            clientId = client.Id,
            lines = new[] { new { description = "Work", quantity = 1m, unitPrice = 1m, taxCategory = "Standard" } },
        });
        (await r.ReadJsonAsync(HttpStatusCode.Conflict)).GetProperty("code").GetString().ShouldBe("settings_incomplete");
    }

    [Fact]
    public async Task Invalid_lines_are_reported_per_field()
    {
        var client = await Scenario.ClientAsync(_admin, "Validation");
        var r = await _admin.PostAsJsonAsync("/api/invoices", new
        {
            clientId = client.Id,
            lines = new[] { new { description = "", quantity = 0m, unitPrice = 1.005m, taxCategory = "Standard" } },
        });
        var errors = (await r.ReadJsonAsync(HttpStatusCode.BadRequest)).GetProperty("errors");
        errors.TryGetProperty("lines[0].description", out _).ShouldBeTrue();
        errors.TryGetProperty("lines[0].quantity", out _).ShouldBeTrue();
        errors.TryGetProperty("lines[0].unitPrice", out _).ShouldBeTrue();
    }
}
