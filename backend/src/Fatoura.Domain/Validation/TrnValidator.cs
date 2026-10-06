namespace Fatoura.Domain.Validation;

/// <summary>
/// UAE Tax Registration Number: exactly 15 digits. The BRD says TRNs "usually start with 100",
/// but real TRNs (including both in the reference invoice: 105386581000003, 105325228200003)
/// start with other "10x" prefixes, so the rule enforced is 15 digits starting with "10".
/// Spaces and dashes are ignored. Mirrored by <c>clients/shared/src/trn.ts</c>.
/// </summary>
public static class TrnValidator
{
    public const int Length = 15;
    public const string RequiredPrefix = "10";

    public static string Normalize(string? value) =>
        new((value ?? string.Empty).Where(c => c is not (' ' or '-')).ToArray());

    public static bool IsValid(string? value)
    {
        var trn = Normalize(value);
        return trn.Length == Length
            && trn.All(char.IsAsciiDigit)
            && trn.StartsWith(RequiredPrefix, StringComparison.Ordinal);
    }
}
