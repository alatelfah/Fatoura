using System.Globalization;
using System.Text.RegularExpressions;

namespace Fatoura.Domain.Numbering;

/// <summary>
/// Formats document numbers from a pattern such as <c>INV/{MON}/{YY}{SEQ:4}</c>.
/// Tokens: <c>{YYYY}</c>, <c>{YY}</c>, <c>{MM}</c>, <c>{MON}</c> (JAN..DEC), <c>{DD}</c>,
/// <c>{SEQ}</c> or <c>{SEQ:n}</c> (zero-padded to n digits). Everything else is literal text.
/// Mirrored by <c>clients/shared/src/numbering.ts</c> and verified against <c>spec/numbering-cases.json</c>.
/// </summary>
public static partial class NumberPatternFormatter
{
    public const int MaxLength = 40;

    [GeneratedRegex(@"\{([A-Z]+)(?::(\d+))?\}")]
    private static partial Regex TokenRegex();

    public static string Format(string pattern, DateOnly date, long sequence)
    {
        var errors = Validate(pattern, SequenceReset.Never);
        if (errors.Count > 0)
        {
            throw new ArgumentException(errors[0], nameof(pattern));
        }

        return TokenRegex().Replace(pattern, m => m.Groups[1].Value switch
        {
            "YYYY" => date.Year.ToString("D4", CultureInfo.InvariantCulture),
            "YY" => (date.Year % 100).ToString("D2", CultureInfo.InvariantCulture),
            "MM" => date.Month.ToString("D2", CultureInfo.InvariantCulture),
            "MON" => CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(date.Month).ToUpperInvariant(),
            "DD" => date.Day.ToString("D2", CultureInfo.InvariantCulture),
            "SEQ" => sequence.ToString(
                "D" + (m.Groups[2].Success ? m.Groups[2].Value : "1"), CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException("Unreachable: validated above."),
        });
    }

    /// <summary>The counter partition for a date: a new counter starts when this key changes.</summary>
    public static string ResetKey(SequenceReset reset, DateOnly date) => reset switch
    {
        SequenceReset.Never => "-",
        SequenceReset.Yearly => date.Year.ToString("D4", CultureInfo.InvariantCulture),
        SequenceReset.Monthly => date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        _ => throw new ArgumentOutOfRangeException(nameof(reset)),
    };

    /// <summary>Returns human-readable problems with the pattern; empty when valid.</summary>
    public static IReadOnlyList<string> Validate(string? pattern, SequenceReset reset)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(pattern))
        {
            errors.Add("Pattern is required.");
            return errors;
        }

        if (pattern.Length > MaxLength)
        {
            errors.Add($"Pattern must be at most {MaxLength} characters.");
        }

        var tokens = TokenRegex().Matches(pattern).Select(m => m.Groups[1].Value).ToList();
        foreach (var token in tokens.Where(t => t is not ("YYYY" or "YY" or "MM" or "MON" or "DD" or "SEQ")).Distinct())
        {
            errors.Add($"Unknown token {{{token}}}.");
        }

        foreach (Match m in TokenRegex().Matches(pattern))
        {
            if (m.Groups[1].Value != "SEQ" && m.Groups[2].Success)
            {
                errors.Add($"Only {{SEQ}} accepts a width ({{{m.Groups[1].Value}:{m.Groups[2].Value}}}).");
            }
            else if (m.Groups[2].Success && int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) is < 1 or > 10)
            {
                errors.Add("{SEQ:n} width must be between 1 and 10.");
            }
        }

        var seqCount = tokens.Count(t => t == "SEQ");
        if (seqCount != 1)
        {
            errors.Add("Pattern must contain exactly one {SEQ} or {SEQ:n} token.");
        }

        var literal = TokenRegex().Replace(pattern, string.Empty);
        if (literal.Contains('{') || literal.Contains('}'))
        {
            errors.Add("Braces are only allowed around tokens.");
        }

        var hasYear = tokens.Contains("YYYY") || tokens.Contains("YY");
        var hasMonth = tokens.Contains("MM") || tokens.Contains("MON");
        if (reset == SequenceReset.Yearly && !hasYear)
        {
            errors.Add("A yearly reset needs {YYYY} or {YY} in the pattern so numbers stay unique.");
        }

        if (reset == SequenceReset.Monthly && !(hasYear && hasMonth))
        {
            errors.Add("A monthly reset needs a year token and {MM} or {MON} in the pattern so numbers stay unique.");
        }

        return errors;
    }

    /// <summary>Default patterns: e.g. INV/SEP/260001.</summary>
    public static string DefaultPattern(DocumentType type) => type switch
    {
        DocumentType.Invoice => "INV/{MON}/{YY}{SEQ:4}",
        DocumentType.Quotation => "QUO/{MON}/{YY}{SEQ:4}",
        DocumentType.CreditNote => "CN/{MON}/{YY}{SEQ:4}",
        DocumentType.Purchase => "PUR/{MON}/{YY}{SEQ:4}",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}
