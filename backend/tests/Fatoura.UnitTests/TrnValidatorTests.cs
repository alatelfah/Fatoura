using Fatoura.Domain.Validation;

namespace Fatoura.UnitTests;

public class TrnValidatorTests
{
    public static TheoryData<string> Valid() => From("valid");

    public static TheoryData<string> Invalid() => From("invalid");

    [Theory]
    [MemberData(nameof(Valid))]
    public void Accepts_valid(string trn) => TrnValidator.IsValid(trn).ShouldBeTrue();

    [Theory]
    [MemberData(nameof(Invalid))]
    public void Rejects_invalid(string trn) => TrnValidator.IsValid(trn).ShouldBeFalse();

    [Fact]
    public void Rejects_null() => TrnValidator.IsValid(null).ShouldBeFalse();

    [Fact]
    public void Normalizes_spaces_and_dashes() =>
        TrnValidator.Normalize(" 100-1234 5678-9012 ").ShouldBe("100123456789012");

    private static TheoryData<string> From(string key)
    {
        var data = new TheoryData<string>();
        foreach (var v in SpecFixtures.Load("trn-cases.json").GetProperty(key).EnumerateArray())
        {
            data.Add(v.GetString()!);
        }

        return data;
    }
}
