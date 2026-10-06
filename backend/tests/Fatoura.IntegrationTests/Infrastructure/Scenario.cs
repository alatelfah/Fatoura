using System.Net;
using System.Net.Http.Json;
using Fatoura.Api.Contacts;
using Fatoura.Api.Items;

namespace Fatoura.IntegrationTests.Infrastructure;

/// <summary>Shared setup: company settings from the reference invoice plus a client and items.</summary>
public static class Scenario
{
    public static async Task ConfigureCompanyAsync(HttpClient admin, bool allowNegativeStock = true)
    {
        var r = await admin.PutAsJsonAsync("/api/settings", new
        {
            name = "HRS TECHNICAL SERVICE LLC",
            address = "Business Bay, Dubai",
            emirate = "Dubai",
            phone = "+971547220420",
            email = "info@hrstechnical.com",
            website = "www.hrstechnical.com",
            trn = "105386581000003",
            vatRate = 0.05m,
            paymentTerms = "30% payment at the time of confirmation of order 60% during progress and balance 10% after completion of work",
            completionOfWork = "Within in 30 days after confirmation",
            notes = "The above quoted price is inclusive of fixing material and labor cost.\nThe Contract value and quantities are Lump Sum as agreed.\nExtra charge will be applicable other than above mentioned scope.",
            closingText = "We hope that you will find our price most competitive and look forward your valuable order.",
            allowNegativeStock,
            quotationValidityDays = 30,
        });
        r.EnsureSuccessStatusCode();
    }

    public static async Task<ClientDto> ClientAsync(HttpClient client, string name = "Aura Suites Downtown") =>
        await (await client.PostAsJsonAsync("/api/clients", new
        {
            name,
            address = "Marasi Drive 6B Street- Business Bay Dubai",
            trn = "105325228200003",
        })).ReadAsync<ClientDto>(HttpStatusCode.Created);

    public static async Task<ItemDto> DishwasherAsync(HttpClient admin, decimal openingStock = 0)
    {
        var item = await (await admin.PostAsJsonAsync("/api/items", new
        {
            type = "Product",
            name = "BOMPANI BUILTIN DISHWAHER",
            description = "BOMPANI BUILTIN DISHWAHER Stainless steel 14 Place settings, 3 Layers of Baskets, Half Load Wash Brand: BOMPANI BO5171N",
            unitPrice = 2050m,
            taxCategory = "Standard",
            trackStock = true,
            reorderLevel = 2m,
        })).ReadAsync<ItemDto>(HttpStatusCode.Created);
        if (openingStock != 0)
        {
            (await admin.PostAsJsonAsync($"/api/items/{item.Id}/adjust-stock", new { quantity = openingStock, note = "Opening" })).EnsureSuccessStatusCode();
        }

        return item;
    }

    public static async Task<ItemDto> InstallationAsync(HttpClient admin) =>
        await (await admin.PostAsJsonAsync("/api/items", new
        {
            type = "Service",
            name = "Supply & installation per piece",
            unitPrice = 250m,
            taxCategory = "Standard",
            trackStock = false,
            reorderLevel = 0m,
        })).ReadAsync<ItemDto>(HttpStatusCode.Created);

    /// <summary>The two lines of the reference invoice (Aura Suites Downtown 260819).</summary>
    public static object[] ReferenceLines(ItemDto dishwasher, ItemDto installation) =>
    [
        new { itemId = dishwasher.Id, description = dishwasher.Description, quantity = 20m, unitPrice = 2050m, taxCategory = "Standard" },
        new { itemId = installation.Id, description = installation.Name, quantity = 20m, unitPrice = 250m, taxCategory = "Standard" },
    ];
}
