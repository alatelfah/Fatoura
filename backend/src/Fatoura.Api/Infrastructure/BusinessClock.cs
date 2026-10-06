namespace Fatoura.Api.Infrastructure;

/// <summary>
/// Business dates are UAE dates (Asia/Dubai, UTC+4, no DST) regardless of the server's time zone,
/// so "today", document dates and the cashier's shift are correct on a UTC container.
/// </summary>
public sealed class BusinessClock(TimeProvider time)
{
    private static readonly TimeZoneInfo Dubai = ResolveDubai();

    public DateTimeOffset Now => time.GetUtcNow();

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), Dubai).DateTime);

    /// <summary>UTC instant at which the given UAE calendar day starts.</summary>
    public static DateTimeOffset StartOfDayUtc(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), Dubai.BaseUtcOffset).ToUniversalTime();

    private static TimeZoneInfo ResolveDubai()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Dubai");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("Asia/Dubai", TimeSpan.FromHours(4), "Gulf Standard Time", "GST");
        }
    }
}
