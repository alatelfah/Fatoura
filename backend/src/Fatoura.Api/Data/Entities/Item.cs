using Fatoura.Domain.Tax;

namespace Fatoura.Api.Data.Entities;

public sealed class Item : IAudited
{
    public int Id { get; set; }

    public ItemType Type { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public TaxCategory TaxCategory { get; set; } = TaxCategory.Standard;

    /// <summary>Only products can track stock.</summary>
    public bool TrackStock { get; set; }

    /// <summary>Cached sum of StockMovements; updated atomically with each movement.</summary>
    public decimal StockQty { get; set; }

    public decimal ReorderLevel { get; set; }

    /// <summary>Moving-average purchase cost (excl. VAT).</summary>
    public decimal AvgCost { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class StockMovement
{
    public long Id { get; set; }

    public int ItemId { get; set; }

    public Item? Item { get; set; }

    public DateTimeOffset At { get; set; }

    /// <summary>Signed: positive adds stock, negative removes it.</summary>
    public decimal Quantity { get; set; }

    public StockMovementType Type { get; set; }

    public string Reference { get; set; } = string.Empty;

    public int? InvoiceId { get; set; }

    public int? CreditNoteId { get; set; }

    public int? PurchaseInvoiceId { get; set; }

    public string Note { get; set; } = string.Empty;

    public Guid? CreatedById { get; set; }
}
