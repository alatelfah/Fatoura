using System.Globalization;
using Fatoura.Domain.Numbering;

namespace Fatoura.UnitTests;

public class NumberPatternFormatterTests
{
    public static TheoryData<string, string, long, string> FormatCases()
    {
        var data = new TheoryData<string, string, long, string>();
        foreach (var c in SpecFixtures.Load("numbering-cases.json").GetProperty("format").EnumerateArray())
        {
            data.Add(c.GetProperty("pattern").GetString()!, c.GetProperty("date").GetString()!,
                c.GetProperty("seq").GetInt64(), c.GetProperty("expected").GetString()!);
        }

        return data;
    }

    public static TheoryData<string, string, string> ResetCases()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var c in SpecFixtures.Load("numbering-cases.json").GetProperty("resetKey").EnumerateArray())
        {
            data.Add(c.GetProperty("reset").GetString()!, c.GetProperty("date").GetString()!,
                c.GetProperty("expected").GetString()!);
        }

        return data;
    }

    public static TheoryData<string, string> Invalid() => PatternCases("invalid");

    public static TheoryData<string, string> Valid() => PatternCases("valid");

    [Theory]
    [MemberData(nameof(FormatCases))]
    public void Formats(string pattern, string date, long seq, string expected) =>
        NumberPatternFormatter.Format(pattern, DateOnly.Parse(date, CultureInfo.InvariantCulture), seq)
            .ShouldBe(expected);

    [Theory]
    [MemberData(nameof(ResetCases))]
    public void Computes_reset_key(string reset, string date, string expected) =>
        NumberPatternFormatter.ResetKey(Enum.Parse<SequenceReset>(reset), DateOnly.Parse(date, CultureInfo.InvariantCulture))
            .ShouldBe(expected);

    [Theory]
    [MemberData(nameof(Invalid))]
    public void Rejects_invalid_patterns(string pattern, string reset) =>
        NumberPatternFormatter.Validate(pattern, Enum.Parse<SequenceReset>(reset)).ShouldNotBeEmpty();

    [Theory]
    [MemberData(nameof(Valid))]
    public void Accepts_valid_patterns(string pattern, string reset) =>
        NumberPatternFormatter.Validate(pattern, Enum.Parse<SequenceReset>(reset)).ShouldBeEmpty();

    [Theory]
    [InlineData(DocumentType.Invoice)]
    [InlineData(DocumentType.Quotation)]
    [InlineData(DocumentType.CreditNote)]
    [InlineData(DocumentType.Purchase)]
    public void Default_patterns_are_valid_for_yearly_reset(DocumentType type) =>
        NumberPatternFormatter.Validate(NumberPatternFormatter.DefaultPattern(type), SequenceReset.Yearly).ShouldBeEmpty();

    private static TheoryData<string, string> PatternCases(string key)
    {
        var data = new TheoryData<string, string>();
        foreach (var c in SpecFixtures.Load("numbering-cases.json").GetProperty(key).EnumerateArray())
        {
            data.Add(c.GetProperty("pattern").GetString()!, c.GetProperty("reset").GetString()!);
        }

        return data;
    }
}
