namespace Fatoura.Api.Data.Entities;

/// <summary>
/// A foreign currency documents may be issued in, and its current rate in AED per unit (the base currency, AED, is
/// implicit). Documents copy the rate when created, so changing it never alters existing documents.
/// </summary>
public sealed class CurrencyRate : IAudited
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal RateToAed { get; set; }

    /// <summary>Inactive currencies are kept for existing documents but cannot be picked for new ones.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; }
}
