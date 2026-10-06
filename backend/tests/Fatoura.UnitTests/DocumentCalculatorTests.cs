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

        var result = DocumentCalculator.Calculate(lines, rate);
        var expected = c.GetProperty("expected");

        var expectedLines = expected.GetProperty("lines").EnumerateArray().ToList();
        result.Lines.Count.ShouldBe(expectedLines.Count);
        for (var i = 0; i < expectedLines.Count; i++)
        {
            result.Lines[i].Net.ShouldBe(expectedLines[i].GetProperty("net").Dec(), $"line {i + 1} net");
            result.Lines[i].Vat.ShouldBe(expectedLines[i].GetProperty("vat").Dec(), $"line {i + 1} vat");
            result.Lines[i].Total.ShouldBe(expectedLines[i].GetProperty("total").Dec(), $"line {i + 1} total");
        }

        result.SubTotal.ShouldBe(expected.GetProperty("subTotal").Dec());
        result.VatTotal.ShouldBe(expected.GetProperty("vatTotal").Dec());
        result.Total.ShouldBe(expected.GetProperty("total").Dec());
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
