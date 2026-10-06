using System.Net;
using System.Net.Http.Json;
using Fatoura.Api.Settings;
using Fatoura.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Fatoura.IntegrationTests;

public class SettingsTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    public static object CompanyRequest(string trn = "105386581000003") => new
    {
        name = "HRS TECHNICAL SERVICE LLC",
        address = "Business Bay, Dubai",
        emirate = "Dubai",
        phone = "+971547220420",
        email = "info@hrstechnical.com",
        website = "www.hrstechnical.com",
        trn,
        vatRate = 0.05m,
        paymentTerms = "30% payment at the time of confirmation of order 60% during progress and balance 10% after completion of work",
        completionOfWork = "Within in 30 days after confirmation",
        notes = "The above quoted price is inclusive of fixing material and labor cost.\nExtra charge will be applicable other than above mentioned scope.",
        closingText = "We hope that you will find our price most competitive.",
        allowNegativeStock = true,
        quotationValidityDays = 30,
    };

    [Fact]
    public async Task Fresh_install_is_incomplete_until_company_details_are_saved()
    {
        var admin = await api.AdminAsync();
        var before = await (await admin.GetAsync("/api/settings")).ReadAsync<SettingsDto>();
        before.VatRate.ShouldBe(0.05m);

        var saved = await (await admin.PutAsJsonAsync("/api/settings", CompanyRequest("105 3865 8100 0003"))).ReadAsync<SettingsDto>();
        saved.IsComplete.ShouldBeTrue();
        saved.Trn.ShouldBe("105386581000003");

        var audit = await api.WithDbAsync(db => db.AuditLogs.Where(a => a.EntityType == "CompanySettings").ToListAsync());
        audit.ShouldContain(a => a.Changes.Contains("HRS TECHNICAL SERVICE LLC"));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("205386581000003")]
    [InlineData("10538658100000X")]
    public async Task Invalid_company_trn_is_rejected(string trn)
    {
        var admin = await api.AdminAsync();
        var r = await admin.PutAsJsonAsync("/api/settings", CompanyRequest(trn));
        (await r.ReadJsonAsync(HttpStatusCode.BadRequest)).GetProperty("errors").TryGetProperty("trn", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Cashier_can_read_but_not_change_settings()
    {
        var (cashier, _, _) = await api.NewCashierAsync();
        (await cashier.GetAsync("/api/settings")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await cashier.PutAsJsonAsync("/api/settings", CompanyRequest())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await cashier.GetAsync("/api/settings/numbering")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await cashier.GetAsync("/api/settings/stamp")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Logo_upload_accepts_png_rejects_other_files_and_is_publicly_readable()
    {
        var admin = await api.AdminAsync();
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
        using (var form = new MultipartFormDataContent { { new ByteArrayContent(png), "file", "logo.png" } })
        {
            (await admin.PutAsync("/api/settings/logo", form)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var anonymous = api.CreateClient();
        var logo = await anonymous.GetAsync("/api/settings/logo");
        logo.StatusCode.ShouldBe(HttpStatusCode.OK);
        logo.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        (await logo.Content.ReadAsByteArrayAsync()).ShouldBe(png);

        using (var form = new MultipartFormDataContent { { new ByteArrayContent("GIF89a"u8.ToArray()), "file", "logo.gif" } })
        {
            (await admin.PutAsync("/api/settings/logo", form)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        (await admin.DeleteAsync("/api/settings/logo")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await anonymous.GetAsync("/api/settings/logo")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Numbering_patterns_are_validated_and_previewed()
    {
        var admin = await api.AdminAsync();
        var defaults = await (await admin.GetAsync("/api/settings/numbering")).ReadAsync<List<NumberingDto>>();
        defaults.Count.ShouldBe(4);

        var bad = await admin.PutAsJsonAsync("/api/settings/numbering", new[] { new { documentType = "Invoice", pattern = "INV-{SEQ}", reset = "Monthly" } });
        bad.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var ok = await (await admin.PutAsJsonAsync("/api/settings/numbering", new[] { new { documentType = "Invoice", pattern = "RIHM/{MON}/{YY}{SEQ:4}", reset = "Yearly" } }))
            .ReadAsync<List<NumberingDto>>();
        ok.Single(n => n.DocumentType == Fatoura.Domain.Numbering.DocumentType.Invoice).Example.ShouldStartWith("RIHM/");
    }
}
