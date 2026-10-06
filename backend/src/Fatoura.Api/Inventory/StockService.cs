using Fatoura.Api.Data;
using Fatoura.Api.Data.Entities;
using Fatoura.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Fatoura.Api.Inventory;

/// <summary>A signed stock change: positive adds stock, negative removes it.</summary>
public sealed record StockChange(
    int ItemId,
    decimal Quantity,
    StockMovementType Type,
    string Reference,
    int? InvoiceId = null,
    int? CreditNoteId = null,
    int? PurchaseInvoiceId = null,
    decimal? UnitCost = null,
    string Note = "");

public sealed record StockWarning(int ItemId, string ItemName, decimal StockAfter);

/// <summary>
/// Applies stock changes for items that track stock. Each change appends a <see cref="StockMovement"/> (the ledger)
/// and atomically updates the cached <see cref="Item.StockQty"/> with a single UPDATE (no read-then-write race).
/// Purchases also update the moving-average cost. Must run inside the caller's transaction; the caller saves.
/// </summary>
public sealed class StockService(FatouraDbContext db, ICurrentUser currentUser, TimeProvider time)
{
    private sealed record UpdatedStock(decimal StockQty, string Name);

    public async Task<IReadOnlyList<StockWarning>> ApplyAsync(IEnumerable<StockChange> changes, bool allowNegative, CancellationToken ct)
    {
        var list = changes.Where(c => c.Quantity != 0).ToList();
        if (list.Count == 0)
        {
            return [];
        }

        var itemIds = list.Select(c => c.ItemId).Distinct().ToList();
        var tracked = await db.Items.Where(i => itemIds.Contains(i.Id) && i.TrackStock).Select(i => i.Id).ToListAsync(ct);
        var warnings = new List<StockWarning>();
        var now = time.GetUtcNow();

        // Lock rows in a consistent (ascending id) order to avoid deadlocks between concurrent documents.
        foreach (var change in list.Where(c => tracked.Contains(c.ItemId)).OrderBy(c => c.ItemId))
        {
            var cost = change.Quantity > 0 ? change.UnitCost : null;
            var rows = await db.Database.SqlQuery<UpdatedStock>($"""
                UPDATE [Items]
                SET [AvgCost] = CASE
                        WHEN {cost} IS NULL THEN [AvgCost]
                        WHEN [StockQty] <= 0 THEN {cost}
                        ELSE ROUND(([StockQty] * [AvgCost] + {change.Quantity} * {cost}) / ([StockQty] + {change.Quantity}), 4)
                    END,
                    [StockQty] = [StockQty] + {change.Quantity}
                OUTPUT inserted.[StockQty], inserted.[Name]
                WHERE [Id] = {change.ItemId}
                """).ToListAsync(ct);
            var updated = rows.Single();

            if (change.Quantity < 0 && updated.StockQty < 0)
            {
                if (!allowNegative)
                {
                    throw new ConflictException(
                        $"Not enough stock for \"{updated.Name}\" ({updated.StockQty - change.Quantity:0.###} available).",
                        "insufficient_stock");
                }

                warnings.Add(new StockWarning(change.ItemId, updated.Name, updated.StockQty));
            }

            db.StockMovements.Add(new StockMovement
            {
                ItemId = change.ItemId,
                At = now,
                Quantity = change.Quantity,
                Type = change.Type,
                Reference = change.Reference,
                InvoiceId = change.InvoiceId,
                CreditNoteId = change.CreditNoteId,
                PurchaseInvoiceId = change.PurchaseInvoiceId,
                Note = change.Note,
                CreatedById = currentUser.Id,
            });
        }

        return warnings;
    }

    /// <summary>Current moving-average cost of the given items (0 for items without purchases).</summary>
    public Task<Dictionary<int, decimal>> CostsAsync(IEnumerable<int> itemIds, CancellationToken ct)
    {
        var ids = itemIds.Distinct().ToList();
        return db.Items.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.AvgCost, ct);
    }
}
