using System.ComponentModel.DataAnnotations;
using Fatoura.Api.Auth;
using Fatoura.Api.Data;
using Fatoura.Api.Data.Entities;
using Fatoura.Api.Infrastructure;
using Fatoura.Api.Inventory;
using Fatoura.Domain.Documents;
using Fatoura.Domain.Tax;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Fatoura.Api.Items;

public sealed record ItemDto(
    int Id,
    ItemType Type,
    string Name,
    string Description,
    decimal UnitPrice,
    TaxCategory TaxCategory,
    bool TrackStock,
    decimal StockQty,
    decimal ReorderLevel,
    decimal AvgCost,
    bool IsLowStock,
    bool IsActive);

public sealed record ItemRequest(
    ItemType Type,
    [property: Required, MaxLength(200)] string Name,
    [property: MaxLength(1000)] string? Description,
    [property: Range(0, 999_999_999)] decimal UnitPrice,
    TaxCategory TaxCategory,
    bool TrackStock,
    [property: Range(0, 999_999_999)] decimal ReorderLevel,
    bool IsActive = true);

public sealed record StockAdjustmentRequest(decimal Quantity, [property: Required, MaxLength(500)] string Note);

public sealed record StockMovementDto(long Id, DateTimeOffset At, decimal Quantity, StockMovementType Type, string Reference, string Note);

public static class ItemEndpoints
{
    public static RouteGroupBuilder MapItemEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/items").WithTags("Items");
        group.MapGet("/", List).RequireAuthorization(Policies.Staff);
        group.MapGet("/{id:int}", Get).RequireAuthorization(Policies.Staff);
        group.MapPost("/", Create).RequireAuthorization(Policies.Admin);
        group.MapPut("/{id:int}", Update).RequireAuthorization(Policies.Admin);
        group.MapDelete("/{id:int}", Delete).RequireAuthorization(Policies.Admin);
        group.MapPost("/{id:int}/adjust-stock", AdjustStock).RequireAuthorization(Policies.Admin);
        group.MapGet("/{id:int}/movements", Movements).RequireAuthorization(Policies.Admin);
        return group;
    }

    public static bool IsLow(Item i) => i.TrackStock && i.StockQty <= i.ReorderLevel;

    private static ItemDto ToDto(Item i) => new(
        i.Id, i.Type, i.Name, i.Description, i.UnitPrice, i.TaxCategory, i.TrackStock, i.StockQty, i.ReorderLevel, i.AvgCost, IsLow(i), i.IsActive);

    private static async Task<Ok<PagedResult<ItemDto>>> List(
        FatouraDbContext db, string? search, ItemType? type, bool? lowStock, bool? includeInactive, int? page, int? pageSize, CancellationToken ct)
    {
        var q = db.Items.AsNoTracking();
        if (includeInactive != true)
        {
            q = q.Where(i => i.IsActive);
        }

        if (type is { } t)
        {
            q = q.Where(i => i.Type == t);
        }

        if (lowStock == true)
        {
            q = q.Where(i => i.TrackStock && i.StockQty <= i.ReorderLevel);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = Paging.Like(search);
            q = q.Where(i => EF.Functions.Like(i.Name, like) || EF.Functions.Like(i.Description, like));
        }

        var result = await q.OrderBy(i => i.Name).ThenBy(i => i.Id)
            .Select(i => new ItemDto(i.Id, i.Type, i.Name, i.Description, i.UnitPrice, i.TaxCategory, i.TrackStock, i.StockQty,
                i.ReorderLevel, i.AvgCost, i.TrackStock && i.StockQty <= i.ReorderLevel, i.IsActive))
            .ToPagedAsync(page, pageSize, ct);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<ItemDto>, NotFound>> Get(int id, FatouraDbContext db, CancellationToken ct) =>
        await db.Items.FindAsync([id], ct) is { } i ? TypedResults.Ok(ToDto(i)) : TypedResults.NotFound();

    private static async Task<Created<ItemDto>> Create(ItemRequest r, FatouraDbContext db, TimeProvider time, CancellationToken ct)
    {
        var item = new Item { CreatedAt = time.GetUtcNow() };
        Apply(r, item);
        item.UpdatedAt = item.CreatedAt;
        db.Items.Add(item);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/items/{item.Id}", ToDto(item));
    }

    private static async Task<Results<Ok<ItemDto>, NotFound>> Update(int id, ItemRequest r, FatouraDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (await db.Items.FindAsync([id], ct) is not { } item)
        {
            return TypedResults.NotFound();
        }

        if (item.TrackStock && !r.TrackStock && item.StockQty != 0)
        {
            throw InvalidRequestException.For("trackStock", "Adjust stock to zero before turning off stock tracking.");
        }

        Apply(r, item);
        item.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(item));
    }

    /// <summary>Deletes an unused item; an item used on documents or with stock history is deactivated instead.</summary>
    private static async Task<Results<NoContent, NotFound>> Delete(int id, FatouraDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (await db.Items.FindAsync([id], ct) is not { } item)
        {
            return TypedResults.NotFound();
        }

        var used = await db.InvoiceLines.AnyAsync(l => l.ItemId == id, ct)
            || await db.QuotationLines.AnyAsync(l => l.ItemId == id, ct)
            || await db.PurchaseLines.AnyAsync(l => l.ItemId == id, ct)
            || await db.StockMovements.AnyAsync(m => m.ItemId == id, ct);
        if (used)
        {
            item.IsActive = false;
            item.UpdatedAt = time.GetUtcNow();
        }
        else
        {
            db.Items.Remove(item);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<ItemDto>, NotFound>> AdjustStock(
        int id, StockAdjustmentRequest r, FatouraDbContext db, StockService stock, CancellationToken ct)
    {
        new FieldValidator()
            .Require(r.Quantity != 0, "quantity", "Quantity must not be zero.")
            .Require(DocumentCalculator.HasAtMostDecimals(r.Quantity, DocumentCalculator.QuantityDecimals), "quantity", "Quantity can have at most 3 decimals.")
            .ThrowIfInvalid();

        var item = await db.Items.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, ct);
        if (item is null)
        {
            return TypedResults.NotFound();
        }

        if (!item.TrackStock)
        {
            throw new ConflictException("This item does not track stock.");
        }

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await stock.ApplyAsync([new StockChange(id, r.Quantity, StockMovementType.Adjustment, "ADJ", Note: r.Note.Trim())], allowNegative: true, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        return TypedResults.Ok(ToDto(await db.Items.AsNoTracking().SingleAsync(i => i.Id == id, ct)));
    }

    private static async Task<Ok<PagedResult<StockMovementDto>>> Movements(int id, FatouraDbContext db, int? page, int? pageSize, CancellationToken ct)
    {
        var result = await db.StockMovements.AsNoTracking().Where(m => m.ItemId == id)
            .OrderByDescending(m => m.At).ThenByDescending(m => m.Id)
            .Select(m => new StockMovementDto(m.Id, m.At, m.Quantity, m.Type, m.Reference, m.Note))
            .ToPagedAsync(page, pageSize, ct);
        return TypedResults.Ok(result);
    }

    private static void Apply(ItemRequest r, Item item)
    {
        new FieldValidator()
            .Require(!string.IsNullOrWhiteSpace(r.Name), "name", "Name is required.")
            .Require(DocumentCalculator.HasAtMostDecimals(r.UnitPrice, DocumentCalculator.MoneyDecimals), "unitPrice", "Unit price can have at most 2 decimals.")
            .Require(!(r.TrackStock && r.Type == ItemType.Service), "trackStock", "Only products can track stock.")
            .ThrowIfInvalid();

        item.Type = r.Type;
        item.Name = r.Name.Trim();
        item.Description = r.Description?.Trim() ?? string.Empty;
        item.UnitPrice = r.UnitPrice;
        item.TaxCategory = r.TaxCategory;
        item.TrackStock = r.TrackStock;
        item.ReorderLevel = r.TrackStock ? r.ReorderLevel : 0;
        item.IsActive = r.IsActive;
    }
}
