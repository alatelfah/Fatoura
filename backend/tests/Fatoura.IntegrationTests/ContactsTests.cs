using System.Net;
using System.Net.Http.Json;
using Fatoura.Api.Contacts;
using Fatoura.Api.Infrastructure;
using Fatoura.IntegrationTests.Infrastructure;

namespace Fatoura.IntegrationTests;

public class ContactsTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Cashier_can_add_and_search_clients_but_not_edit_or_delete()
    {
        var (cashier, _, _) = await api.NewCashierAsync();
        var created = await (await cashier.PostAsJsonAsync("/api/clients", new
        {
            name = "Aura Suites Downtown",
            address = "Marasi Drive 6B Street- Business Bay Dubai",
            trn = "105325228200003",
        })).ReadAsync<ClientDto>(HttpStatusCode.Created);
        created.Trn.ShouldBe("105325228200003");

        var page = await (await cashier.GetAsync("/api/clients?search=aura")).ReadAsync<PagedResult<ClientDto>>();
        page.Items.ShouldContain(c => c.Id == created.Id);

        (await cashier.PutAsJsonAsync($"/api/clients/{created.Id}", new { name = "Renamed" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await cashier.DeleteAsync($"/api/clients/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Client_trn_is_optional_but_validated()
    {
        var admin = await api.AdminAsync();
        (await admin.PostAsJsonAsync("/api/clients", new { name = "Walk-in" })).StatusCode.ShouldBe(HttpStatusCode.Created);
        var bad = await admin.PostAsJsonAsync("/api/clients", new { name = "Bad TRN", trn = "999" });
        (await bad.ReadJsonAsync(HttpStatusCode.BadRequest)).GetProperty("errors").TryGetProperty("trn", out _).ShouldBeTrue();
        (await admin.PostAsJsonAsync("/api/clients", new { name = "", trn = "" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/clients", new { name = "Bad email", email = "nope" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_treats_like_wildcards_literally()
    {
        var admin = await api.AdminAsync();
        await admin.PostAsJsonAsync("/api/clients", new { name = "100% Cotton LLC" });
        await admin.PostAsJsonAsync("/api/clients", new { name = "100 Cotton Traders" });
        var page = await (await admin.GetAsync("/api/clients?search=100%25")).ReadAsync<PagedResult<ClientDto>>();
        page.Items.ShouldAllBe(c => c.Name.Contains("100%"));
    }

    [Fact]
    public async Task Suppliers_are_admin_only_and_unused_ones_are_deleted()
    {
        var (cashier, _, _) = await api.NewCashierAsync();
        (await cashier.GetAsync("/api/suppliers")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var admin = await api.AdminAsync();
        var s = await (await admin.PostAsJsonAsync("/api/suppliers", new { name = "BOMPANI Trading", trn = "100123456789012", phone = "+97140000000" }))
            .ReadAsync<SupplierDto>(HttpStatusCode.Created);
        var updated = await (await admin.PutAsJsonAsync($"/api/suppliers/{s.Id}", new { name = "BOMPANI Trading LLC", trn = "100123456789012" }))
            .ReadAsync<SupplierDto>();
        updated.Name.ShouldBe("BOMPANI Trading LLC");

        (await admin.DeleteAsync($"/api/suppliers/{s.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.GetAsync($"/api/suppliers/{s.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
