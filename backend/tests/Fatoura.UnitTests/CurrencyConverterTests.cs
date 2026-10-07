using Fatoura.Domain.Documents;
using Fatoura.Domain.Tax;

namespace Fatoura.UnitTests;

public class CurrencyConverterTests
{
    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();
        foreach (var c in SpecFixtures.Load("currency-cases.json").GetProperty("cases").EnumerateArray())
        {
            data.Add(c.GetProperty("name").GetString()!);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Matches_shared_fixture(string name)
    {
        var root = SpecFixtures.Load("currency-cases.json");
        var c = root.GetProperty("cases").EnumerateArray().Single(x => x.GetProperty("name").GetString() == name);
        var lines = c.GetProperty("lines").EnumerateArray()
            .Select(l => new LineInput(l.GetProperty("qty").Dec(), l.GetProperty("unitPrice").Dec(), Enum.Parse<TaxCategory>(l.GetProperty("tax").GetString()!)))
            .ToList();

        var totals = DocumentCalculator.Calculate(lines, root.GetProperty("standardRate").Dec());
        var aed = CurrencyConverter.ToAed(totals.Lines, c.GetProperty("exchangeRate").Dec());

        var expected = c.GetProperty("expected");
        (totals.SubTotal, totals.VatTotal, totals.Total).ShouldBe(
            (expected.GetProperty("subTotal").Dec(), expected.GetProperty("vatTotal").Dec(), expected.GetProperty("total").Dec()));
        var e = expected.GetProperty("aed");
        aed.Lines.Select(l => (l.Net, l.Vat)).ShouldBe(
            e.GetProperty("lines").EnumerateArray().Select(l => (l.GetProperty("net").Dec(), l.GetProperty("vat").Dec())));
        (aed.SubTotal, aed.VatTotal, aed.Total).ShouldBe((e.GetProperty("subTotal").Dec(), e.GetProperty("vatTotal").Dec(), e.GetProperty("total").Dec()));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("3.6725001")]
    [InlineData("100000.01")]
    public void Rejects_invalid_rates(string rate) =>
        Should.Throw<ArgumentOutOfRangeException>(() => CurrencyConverter.ToAed(
            DocumentCalculator.Calculate([new LineInput(1, 10m, TaxCategory.Standard)], 0.05m).Lines,
            decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("USD", true)]
    [InlineData("usd", false)]
    [InlineData("US", false)]
    [InlineData("US1", false)]
    [InlineData(null, false)]
    public void Validates_iso_codes(string? code, bool valid) => CurrencyConverter.IsValidCode(code).ShouldBe(valid);
}
