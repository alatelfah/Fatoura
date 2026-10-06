namespace Fatoura.Domain.Tax;

/// <summary>
/// UAE VAT treatment of a supply. Only <see cref="Standard"/> carries VAT (5% by default);
/// zero-rated and exempt supplies are reported in separate boxes of the VAT 201 return.
/// </summary>
public enum TaxCategory
{
    Standard = 0,
    ZeroRated = 1,
    Exempt = 2,
}
