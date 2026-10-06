using System.Net;
using System.Net.Http.Json;
using Fatoura.Api.Infrastructure;
using Fatoura.Api.Items;
using Fatoura.Domain.Tax;
using Fatoura.IntegrationTests.Infrastructure;

namespace Fatoura.IntegrationTests;

public class ItemsTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    public static object Product(string name = "BOMPANI BUILTIN DISHWASHER", decimal price = 2050m, decimal reorder = 5m) => new
    {
        type = "Product",
        name,
        description = "Stainless steel 14 Place settings, 3 Layers of Baskets, Half Load Wash Brand: BOMPANI BO5171N",
        unitPrice = price,
        taxCategory = "Standard",
        trackStock = true,
        reorderLevel = reorder,
    };

    [Fact]
    public async Task Admin_manages_items_and_cashier_can_only_read()
    {
        var admin = await api.AdminAsync();
        var item = await (await admin.PostAsJsonAsync("/api/items", Product())).ReadAsync<ItemDto>(HttpStatusCode.Created);
        item.TaxCategory.ShouldBe(TaxCategory.Standard);
        item.IsLowStock.ShouldBeTrue(); // 0 on hand <= reorder level 5

        var (cashier, _, _) = await api.NewCashierAsync();
        (await cashier.GetAsync($"/api/items/{item.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await cashier.PostAsJsonAsync("/api/items", Product("Nope"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await cashier.PostAsJsonAsync($"/api/items/{item.Id}/adjust-stock", new { quantity = 5, note = "x" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Services_cannot_track_stock_and_prices_have_two_decimals()
    {
        var admin = await api.AdminAsync();
        var service = await admin.PostAsJsonAsync("/api/items", new { type = "Service", name = "Supply & installation per piece", unitPrice = 250m, taxCategory = "Standard", trackStock = true, reorderLevel = 0 });
        service.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var price = await admin.PostAsJsonAsync("/api/items", new { type = "Service", name = "Odd price", unitPrice = 1.005m, taxCategory = "Standard", trackStock = false, reorderLevel = 0 });
        price.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Stock_adjustments_are_recorded_in_the_ledger()
    {
        var admin = await api.AdminAsync();
        var item = await (await admin.PostAsJsonAsync("/api/items", Product("Ledger item"))).ReadAsync<ItemDto>(HttpStatusCode.Created);

        var after = await (await admin.PostAsJsonAsync($"/api/items/{item.Id}/adjust-stock", new { quantity = 12.5m, note = "Opening stock" })).ReadAsync<ItemDto>();
        after.StockQty.ShouldBe(12.5m);
        after.IsLowStock.ShouldBeFalse();

        after = await (await admin.PostAsJsonAsync($"/api/items/{item.Id}/adjust-stock", new { quantity = -10m, note = "Damaged" })).ReadAsync<ItemDto>();
        after.StockQty.ShouldBe(2.5m);
        after.IsLowStock.ShouldBeTrue();

        var movements = await (await admin.GetAsync($"/api/items/{item.Id}/movements")).ReadAsync<PagedResult<StockMovementDto>>();
        movements.Items.Select(m => m.Quantity).ShouldBe([-10m, 12.5m]);
        movements.Items[0].Note.ShouldBe("Damaged");

        var low = await (await admin.GetAsync("/api/items?lowStock=true")).ReadAsync<PagedResult<ItemDto>>();
        low.Items.ShouldContain(i => i.Id == item.Id);
    }

    [Fact]
    public async Task Concurrent_adjustments_do_not_lose_updates()
    {
        var admin = await api.AdminAsync();
        var item = await (await admin.PostAsJsonAsync("/api/items", Product("Race item"))).ReadAsync<ItemDto>(HttpStatusCode.Created);

        var tasks = Enumerable.Range(0, 20).Select(_ => admin.PostAsJsonAsync($"/api/items/{item.Id}/adjust-stock", new { quantity = 1m, note = "+1" }));
        foreach (var r in await Task.WhenAll(tasks))
        {
            r.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var final = await (await admin.GetAsync($"/api/items/{item.Id}")).ReadAsync<ItemDto>();
        final.StockQty.ShouldBe(20m);
    }
}
