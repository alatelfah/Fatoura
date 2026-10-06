using System.Net;
using System.Net.Http.Json;
using System.Text;
using ClosedXML.Excel;
using Fatoura.Api.Documents;
using Fatoura.Api.Pdf;
using Fatoura.IntegrationTests.Infrastructure;

namespace Fatoura.IntegrationTests;

public class DocumentOutputTests(ApiFactory api) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private HttpClient _admin = null!;

    public async ValueTask InitializeAsync()
    {
        _admin = await api.AdminAsync();
        await Scenario.ConfigureCompanyAsync(_admin);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Invoice_quotation_and_credit_note_render_as_pdf_for_staff()
    {
        var client = await Scenario.ClientAsync(_admin);
        var dishwasher = await Scenario.DishwasherAsync(_admin, 40);
        var install = await Scenario.InstallationAsync(_admin);
        var invoice = (await (await _admin.PostAsJsonAsync("/api/invoices", new { clientId = client.Id, lines = Scenario.ReferenceLines(dishwasher, install) }))
            .ReadAsync<InvoiceResult>(HttpStatusCode.Created)).Invoice;
        var quote = await (await _admin.PostAsJsonAsync("/api/quotations", new { clientId = client.Id, lines = Scenario.ReferenceLines(dishwasher, install) }))
            .ReadAsync<QuotationDto>(HttpStatusCode.Created);
        var note = await (await _admin.PostAsJsonAsync("/api/credit-notes", new
        {
            invoiceId = invoice.Id, reason = "Return", returnToStock = true, lines = new[] { new { invoiceLineId = invoice.Lines[0].Id, quantity = 1m } },
        })).ReadAsync<CreditNoteDto>(HttpStatusCode.Created);

        var (cashier, _, _) = await api.NewCashierAsync();
        foreach (var url in new[] { $"/api/invoices/{invoice.Id}/pdf", $"/api/quotations/{quote.Id}/pdf", $"/api/credit-notes/{note.Id}/pdf" })
        {
            var r = await cashier.GetAsync(url);
            r.StatusCode.ShouldBe(HttpStatusCode.OK, url);
            r.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
            var bytes = await r.Content.ReadAsByteArrayAsync();
            Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
        }

        var download = await cashier.GetAsync($"/api/invoices/{invoice.Id}/pdf?download=true");
        download.Content.Headers.ContentDisposition!.FileName!.Trim('"').ShouldBe(PdfService.FileName("Invoice", invoice.Number));
        PdfService.FileName("Invoice", invoice.Number).ShouldNotContain("/");

        (await api.CreateClient().GetAsync($"/api/invoices/{invoice.Id}/pdf")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await cashier.GetAsync("/api/invoices/999999/pdf")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public void Long_documents_flow_onto_further_pages_and_arabic_text_renders()
    {
        var lines = Enumerable.Range(1, 60)
            .Select(i => new PrintLine(i, i % 3 == 0 ? $"خدمة رقم {i} - Service line {i}" : $"Service line {i} with a reasonably long description to wrap", 1.5m, 99.99m, 7.50m, 157.48m))
            .ToList();
        lines.Add(new PrintLine(61, new string('X', 1000), 1, 1, 0, 1)); // an unbreakable 1000-character description
        var doc = new PrintDocument(
            "Tax Invoice", "Invoice No:", "INV/OCT/260001", new DateOnly(2026, 10, 1),
            new PrintCompany("Test Co", "Dubai", "+971", "a@b.c", "x.ae", "100123456789012", null, null),
            new PrintParty("شركة الواحة", "دبي", "", ""), [], lines, 8998.20m, "VAT 5%", 449.91m, 9448.11m,
            new PrintTerms("50% advance", "2 weeks", ["One", "Two"], "Thanks"), IsVoid: true);

        var pdf = PdfService.Render(doc);
        var text = Encoding.Latin1.GetString(pdf);
        var pages = System.Text.RegularExpressions.Regex.Matches(text, @"/Type\s*/Page\b").Count;
        pages.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task Reports_export_to_excel_and_respect_roles()
    {
        var client = await Scenario.ClientAsync(_admin, "Excel Client");
        await (await _admin.PostAsJsonAsync("/api/invoices", new
        {
            clientId = client.Id,
            lines = new[] { new { description = "Consulting", quantity = 2m, unitPrice = 1500m, taxCategory = "Standard" } },
        })).ReadAsync<InvoiceResult>(HttpStatusCode.Created);

        foreach (var name in new[] { "sales", "purchases", "profit-loss", "vat" })
        {
            var r = await _admin.GetAsync($"/api/reports/{name}/export");
            r.StatusCode.ShouldBe(HttpStatusCode.OK, name);
            r.Content.Headers.ContentType!.MediaType.ShouldBe(Fatoura.Api.Reports.ReportExcel.ContentType);
            using var wb = new XLWorkbook(await r.Content.ReadAsStreamAsync());
            wb.Worksheets.First().Cell(1, 1).GetString().ShouldBe("HRS TECHNICAL SERVICE LLC");
        }

        using (var wb = new XLWorkbook(await (await _admin.GetAsync("/api/reports/sales/export")).Content.ReadAsStreamAsync()))
        {
            var ws = wb.Worksheets.First();
            var netSalesRow = ws.RowsUsed().First(r => r.Cell(1).GetString() == "Net sales (excl. VAT)");
            netSalesRow.Cell(2).GetValue<decimal>().ShouldBeGreaterThanOrEqualTo(3000m);
        }

        var (cashier, _, _) = await api.NewCashierAsync();
        (await cashier.GetAsync("/api/reports/sales/export")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await cashier.GetAsync("/api/reports/vat/export")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
