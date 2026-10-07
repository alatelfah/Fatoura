namespace Fatoura.Domain.Documents;

public sealed record LineAed(decimal Net, decimal Vat);

public sealed record AedAmounts(IReadOnlyList<LineAed> Lines, decimal SubTotal, decimal VatTotal, decimal Total);

/// <summary>
/// AED equivalents of a document in another currency; the FTA requires VAT to be stated and reported in AED.
/// <c>Sub Total (AED) = round2(Sub Total × rate)</c>, <c>VAT (AED) = round2(Total VAT × rate)</c>,
/// <c>Total (AED) = Sub Total (AED) + VAT (AED)</c>. The AED totals are spread back over the lines (as the discount is)
/// so per-category figures in the VAT return add up to the documents. The rate is AED per one unit of the currency.
/// </summary>
public static class CurrencyConverter
{
    public const string Base = "AED";
    public const int RateDecimals = 6;
    public const decimal MaxRate = 100_000m;

    public static bool IsValidCode(string? code) => code is { Length: 3 } && code.All(c => c is >= 'A' and <= 'Z');

    public static bool IsValidRate(decimal rate) =>
        rate > 0 && rate <= MaxRate && DocumentCalculator.HasAtMostDecimals(rate, RateDecimals);

    public static AedAmounts ToAed(IReadOnlyList<LineAmounts> lines, decimal rate)
    {
        if (!IsValidRate(rate))
        {
            throw new ArgumentOutOfRangeException(nameof(rate), "An exchange rate must be positive with at most 6 decimals.");
        }

        var subTotal = DocumentCalculator.RoundMoney(lines.Sum(l => l.Net) * rate);
        var vatTotal = DocumentCalculator.RoundMoney(lines.Sum(l => l.Vat) * rate);
        var nets = DocumentCalculator.Allocate(subTotal, lines.Select(l => l.Net).ToList());
        var vats = DocumentCalculator.Allocate(vatTotal, lines.Select(l => l.Vat).ToList());
        return new AedAmounts(nets.Zip(vats, (n, v) => new LineAed(n, v)).ToList(), subTotal, vatTotal, subTotal + vatTotal);
    }
}
