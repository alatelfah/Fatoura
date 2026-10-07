using Fatoura.Domain.Documents;
using Fatoura.Domain.Tax;

namespace Fatoura.UnitTests;

public class DocumentCalculatorTests
{
    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();
        foreach (var c in SpecFixtures.Load("calc-cases.json").GetProperty("cases").EnumerateArray())
        {
            data.Add(c.GetProperty("name").GetString()!);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Matches_shared_fixture(string name)
    {
        var root = SpecFixtures.Load("calc-cases.json");
        var rate = root.GetProperty("standardRate").Dec();
        var c = root.GetProperty("cases").EnumerateArray().Single(x => x.GetProperty("name").GetString() == name);

        var lines = c.GetProperty("lines").EnumerateArray()
            .Select(l => new LineInput(
                l.GetProperty("qty").Dec(),
                l.GetProperty("unitPrice").Dec(),
                Enum.Parse<TaxCategory>(l.GetProperty("tax").GetString()!)))
            .ToList();

        var discount = c.TryGetProperty("discount", out var d)
            ? new DocumentDiscount(Enum.Parse<DiscountKind>(d.GetProperty("kind").GetString()!), d.GetProperty("value").Dec())
            : null;
        var result = DocumentCalculator.Calculate(lines, rate, discount);
        var expected = c.GetProperty("expected");

        var expectedLines = expected.GetProperty("lines").EnumerateArray().ToList();
        result.Lines.Count.ShouldBe(expectedLines.Count);
        for (var i = 0; i < expectedLines.Count; i++)
        {
            result.Lines[i].Discount.ShouldBe(Optional(expectedLines[i], "discount"), $"line {i + 1} discount");
            result.Lines[i].Net.ShouldBe(expectedLines[i].GetProperty("net").Dec(), $"line {i + 1} net");
            result.Lines[i].Vat.ShouldBe(expectedLines[i].GetProperty("vat").Dec(), $"line {i + 1} vat");
            result.Lines[i].Total.ShouldBe(expectedLines[i].GetProperty("total").Dec(), $"line {i + 1} total");
        }

        result.Discount.ShouldBe(Optional(expected, "discount"));
        result.GrossSubTotal.ShouldBe(expected.TryGetProperty("grossSubTotal", out var g) ? g.Dec() : expected.GetProperty("subTotal").Dec());
        result.SubTotal.ShouldBe(expected.GetProperty("subTotal").Dec());
        result.VatTotal.ShouldBe(expected.GetProperty("vatTotal").Dec());
        result.Total.ShouldBe(expected.GetProperty("total").Dec());
    }

    private static decimal Optional(System.Text.Json.JsonElement e, string name) =>
        e.TryGetProperty(name, out var value) ? value.Dec() : 0m;

    [Theory]
    [InlineData(DiscountKind.Percent, "100.01")]
    [InlineData(DiscountKind.Percent, "-1")]
    [InlineData(DiscountKind.Percent, "10.005")]
    [InlineData(DiscountKind.Amount, "30.01")]
    [InlineData(DiscountKind.Amount, "-0.01")]
    [InlineData(DiscountKind.Amount, "0.001")]
    public void Rejects_out_of_range_discount(DiscountKind kind, string value) =>
        Should.Throw<ArgumentOutOfRangeException>(() => DocumentCalculator.Calculate(
            [new LineInput(2, 15m, TaxCategory.Standard)], 0.05m,
            new DocumentDiscount(kind, decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture))));

    [Fact]
    public void Discount_shares_always_add_up_and_never_exceed_a_line()
    {
        var random = new Random(42);
        for (var run = 0; run < 500; run++)
        {
            var weights = Enumerable.Range(0, random.Next(1, 8)).Select(_ => random.Next(0, 100_000) / 100m).ToList();
            var amount = Math.Round(weights.Sum() * (decimal)random.NextDouble(), 2, MidpointRounding.ToZero);
            var shares = DocumentCalculator.Allocate(amount, weights);
            shares.Sum().ShouldBe(amount);
            shares.Zip(weights).ShouldAllBe(x => x.First >= 0 && x.First <= x.Second);
        }
    }

    [Fact]
    public void Discount_allocation_handles_the_largest_allowed_amounts()
    {
        // Max quantity × max unit price on many lines must not overflow.
        var lines = Enumerable.Repeat(new LineInput(1_000_000m, 999_999_999m, TaxCategory.Standard), 200).ToList();
        var result = DocumentCalculator.Calculate(lines, 0.05m, new DocumentDiscount(DiscountKind.Percent, 33.33m));
        result.Lines.Sum(l => l.Discount).ShouldBe(result.Discount);
    }

    [Fact]
    public void Standard_rate_vat_equals_brd_formula_when_all_lines_standard()
    {
        // BRD: Total VAT = Sub Total × 0.05 — holds exactly for the reference invoice.
        var result = DocumentCalculator.Calculate(
            [new LineInput(20, 2050m, TaxCategory.Standard), new LineInput(20, 250m, TaxCategory.Standard)], 0.05m);
        result.VatTotal.ShouldBe(result.SubTotal * 0.05m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_non_positive_quantity(decimal qty) =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            DocumentCalculator.CalculateLine(new LineInput(qty, 10m, TaxCategory.Standard), 0.05m));

    [Fact]
    public void Rejects_negative_price() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            DocumentCalculator.CalculateLine(new LineInput(1, -0.01m, TaxCategory.Standard), 0.05m));

    [Theory]
    [InlineData("1.5", 2, true)]
    [InlineData("1.505", 2, false)]
    [InlineData("1.125", 3, true)]
    [InlineData("1.1255", 3, false)]
    public void Detects_excess_decimals(string value, int decimals, bool expected) =>
        DocumentCalculator.HasAtMostDecimals(
            decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture), decimals).ShouldBe(expected);
}
