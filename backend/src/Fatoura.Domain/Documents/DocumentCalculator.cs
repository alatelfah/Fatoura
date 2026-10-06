using Fatoura.Domain.Tax;

namespace Fatoura.Domain.Documents;

public sealed record LineInput(decimal Quantity, decimal UnitPrice, TaxCategory TaxCategory);

public sealed record LineAmounts(decimal Net, decimal VatRate, decimal Vat, decimal Total);

public sealed record DocumentTotals(IReadOnlyList<LineAmounts> Lines, decimal SubTotal, decimal VatTotal, decimal Total);

/// <summary>
/// The single source of truth for document math (BRD §5):
/// <c>Line Net = round2(Qty × Unit Price)</c>, <c>Line VAT = round2(Line Net × rate)</c>,
/// <c>Line Amount = Line Net + Line VAT</c>, <c>Sub Total = Σ Line Net</c>,
/// <c>Total VAT = Σ Line VAT</c>, <c>Total = Sub Total + Total VAT</c>.
/// Rounding is to 2 decimals, half away from zero. Mirrored by <c>clients/shared/src/calc.ts</c>
/// and both are verified against <c>spec/calc-cases.json</c>.
/// </summary>
public static class DocumentCalculator
{
    public const int QuantityDecimals = 3;
    public const int MoneyDecimals = 2;

    public static decimal RoundMoney(decimal value) =>
        Math.Round(value, MoneyDecimals, MidpointRounding.AwayFromZero);

    public static decimal RateFor(TaxCategory category, decimal standardRate) =>
        category == TaxCategory.Standard ? standardRate : 0m;

    public static LineAmounts CalculateLine(LineInput line, decimal standardRate)
    {
        if (line.Quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(line), "Quantity must be greater than zero.");
        }

        if (line.UnitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(line), "Unit price cannot be negative.");
        }

        var rate = RateFor(line.TaxCategory, standardRate);
        var net = RoundMoney(line.Quantity * line.UnitPrice);
        var vat = RoundMoney(net * rate);
        return new LineAmounts(net, rate, vat, net + vat);
    }

    public static DocumentTotals Calculate(IEnumerable<LineInput> lines, decimal standardRate)
    {
        var results = lines.Select(l => CalculateLine(l, standardRate)).ToList();
        var subTotal = results.Sum(r => r.Net);
        var vatTotal = results.Sum(r => r.Vat);
        return new DocumentTotals(results, subTotal, vatTotal, subTotal + vatTotal);
    }

    public static bool HasAtMostDecimals(decimal value, int decimals) =>
        decimal.Round(value, decimals) == value;
}
