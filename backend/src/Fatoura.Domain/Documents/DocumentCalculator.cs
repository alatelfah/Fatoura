using System.Numerics;
using Fatoura.Domain.Tax;

namespace Fatoura.Domain.Documents;

public sealed record LineInput(decimal Quantity, decimal UnitPrice, TaxCategory TaxCategory);

/// <summary>A document-level discount: a fixed amount, or a percentage of the sub total before discount.</summary>
public enum DiscountKind
{
    None = 0,
    Amount = 1,
    Percent = 2,
}

public sealed record DocumentDiscount(DiscountKind Kind, decimal Value)
{
    public static readonly DocumentDiscount None = new(DiscountKind.None, 0m);
}

/// <summary>
/// <paramref name="Net"/> is the taxable value after the line's share of any document discount;
/// <see cref="Gross"/> is <c>round2(Qty × Unit Price)</c> before it.
/// </summary>
public sealed record LineAmounts(decimal Net, decimal VatRate, decimal Vat, decimal Total, decimal Discount = 0m)
{
    public decimal Gross => Net + Discount;
}

/// <summary><paramref name="SubTotal"/> is after the discount (the taxable amount); <see cref="GrossSubTotal"/> is before it.</summary>
public sealed record DocumentTotals(IReadOnlyList<LineAmounts> Lines, decimal SubTotal, decimal VatTotal, decimal Total, decimal Discount = 0m)
{
    public decimal GrossSubTotal => SubTotal + Discount;
}

/// <summary>
/// The single source of truth for document math (BRD §5):
/// <c>Line Gross = round2(Qty × Unit Price)</c>, <c>Line Net = Line Gross − Line Discount</c>,
/// <c>Line VAT = round2(Line Net × rate)</c>, <c>Line Amount = Line Net + Line VAT</c>,
/// <c>Sub Total = Σ Line Net</c>, <c>Total VAT = Σ Line VAT</c>, <c>Total = Sub Total + Total VAT</c>.
/// A document discount is spread over the lines in proportion to their gross amounts, to the fils, so VAT is
/// charged on the discounted value of each line (FTA). Rounding is to 2 decimals, half away from zero.
/// Mirrored by <c>clients/shared/src/calc.ts</c> and both are verified against <c>spec/calc-cases.json</c>.
/// </summary>
public static class DocumentCalculator
{
    public const int QuantityDecimals = 3;
    public const int MoneyDecimals = 2;

    public static decimal RoundMoney(decimal value) =>
        Math.Round(value, MoneyDecimals, MidpointRounding.AwayFromZero);

    public static decimal RateFor(TaxCategory category, decimal standardRate) =>
        category == TaxCategory.Standard ? standardRate : 0m;

    public static decimal Gross(LineInput line)
    {
        if (line.Quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(line), "Quantity must be greater than zero.");
        }

        if (line.UnitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(line), "Unit price cannot be negative.");
        }

        return RoundMoney(line.Quantity * line.UnitPrice);
    }

    public static LineAmounts CalculateLine(LineInput line, decimal standardRate, decimal discount = 0m)
    {
        var gross = Gross(line);
        if (discount < 0 || discount > gross)
        {
            throw new ArgumentOutOfRangeException(nameof(discount), "A line discount must be between zero and the line amount.");
        }

        var rate = RateFor(line.TaxCategory, standardRate);
        var net = gross - discount;
        var vat = RoundMoney(net * rate);
        return new LineAmounts(net, rate, vat, net + vat, discount);
    }

    public static DocumentTotals Calculate(IEnumerable<LineInput> lines, decimal standardRate, DocumentDiscount? discount = null)
    {
        var inputs = lines.ToList();
        var gross = inputs.Select(Gross).ToList();
        var amount = DiscountAmount(discount ?? DocumentDiscount.None, gross.Sum());
        var shares = Allocate(amount, gross);
        var results = inputs.Select((l, i) => CalculateLine(l, standardRate, shares[i])).ToList();
        var subTotal = results.Sum(r => r.Net);
        var vatTotal = results.Sum(r => r.Vat);
        return new DocumentTotals(results, subTotal, vatTotal, subTotal + vatTotal, amount);
    }

    /// <summary>The discount in money: a percentage is <c>round2(gross × % / 100)</c>; an amount may not exceed the gross.</summary>
    public static decimal DiscountAmount(DocumentDiscount discount, decimal grossSubTotal)
    {
        switch (discount.Kind)
        {
            case DiscountKind.None:
                return 0m;
            case DiscountKind.Percent:
                if (discount.Value < 0 || discount.Value > 100 || !HasAtMostDecimals(discount.Value, MoneyDecimals))
                {
                    throw new ArgumentOutOfRangeException(nameof(discount), "A discount percentage must be between 0 and 100 with at most 2 decimals.");
                }

                return RoundMoney(grossSubTotal * discount.Value / 100m);
            case DiscountKind.Amount:
                if (discount.Value < 0 || discount.Value > grossSubTotal || !HasAtMostDecimals(discount.Value, MoneyDecimals))
                {
                    throw new ArgumentOutOfRangeException(nameof(discount), "A discount amount must be between 0 and the sub total with at most 2 decimals.");
                }

                return discount.Value;
            default:
                throw new ArgumentOutOfRangeException(nameof(discount), "Unknown discount kind.");
        }
    }

    /// <summary>
    /// Splits <paramref name="amount"/> over <paramref name="weights"/> proportionally, in whole fils, so the shares
    /// add up exactly (largest remainder; ties go to the earlier line). Integer arithmetic keeps C# and TypeScript identical.
    /// </summary>
    public static IReadOnlyList<decimal> Allocate(decimal amount, IReadOnlyList<decimal> weights)
    {
        static BigInteger Fils(decimal value) => new(value * 100m);

        var shares = new BigInteger[weights.Count];
        var total = weights.Aggregate(BigInteger.Zero, (sum, w) => sum + Fils(w));
        var fils = Fils(amount);
        if (fils.IsZero || total.IsZero)
        {
            return new decimal[weights.Count];
        }

        var remainders = new BigInteger[weights.Count];
        for (var i = 0; i < weights.Count; i++)
        {
            shares[i] = BigInteger.DivRem(fils * Fils(weights[i]), total, out remainders[i]);
        }

        var left = (int)(fils - shares.Aggregate(BigInteger.Zero, (sum, s) => sum + s));
        foreach (var i in Enumerable.Range(0, weights.Count).OrderByDescending(i => remainders[i]).ThenBy(i => i).Take(left))
        {
            shares[i] += 1;
        }

        return shares.Select(s => (decimal)s / 100m).ToList();
    }

    public static bool HasAtMostDecimals(decimal value, int decimals) =>
        decimal.Round(value, decimals) == value;
}
