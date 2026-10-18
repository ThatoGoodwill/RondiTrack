using System.Globalization;

namespace RondiTrack.Domain;

// Every calendar rule for cycles lives here, as pure functions. A stokvel's calendar is South African:
// "the cycle ends on the 25th" means the 25th in Africa/Johannesburg, whatever time zone the server is in.
public static class CycleCalendar
{
    // "Today" in the stokvel's time zone, computed from the injected clock (never from the machine's local time).
    public static DateOnly TodayIn(TimeZoneInfo zone, DateTimeOffset now) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);

    // The instant a cycle closes: 00:00 on the day AFTER endsOn, in the stokvel's zone, expressed in UTC.
    // For Africa/Johannesburg (UTC+2, no daylight saving) a cycle ending on the 25th closes at 22:00 UTC on the 25th.
    public static DateTimeOffset ClosesAtUtc(DateOnly endsOn, TimeZoneInfo zone)
    {
        var localMidnight = endsOn.AddDays(1).ToDateTime(TimeOnly.MinValue);   // Kind = Unspecified
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localMidnight, zone), TimeSpan.Zero);
    }

    public static DateOnly NextEnd(ContributionFrequency frequency, DateOnly previousEnd) => frequency switch
    {
        ContributionFrequency.Weekly => previousEnd.AddDays(7),
        ContributionFrequency.Fortnightly => previousEnd.AddDays(14),
        _ => IsLastDayOfMonth(previousEnd)
                ? LastDayOfMonth(previousEnd.AddMonths(1))
                : previousEnd.AddMonths(1)
    };

    // Catch-up rule: the first period end that is today or later (an end date of today is still open until midnight).
    public static DateOnly NextEndOnOrAfter(ContributionFrequency frequency, DateOnly previousEnd, DateOnly today)
    {
        var next = NextEnd(frequency, previousEnd);
        while (next < today) next = NextEnd(frequency, next);
        return next;
    }

    public static string LabelFor(ContributionFrequency frequency, DateOnly endsOn)
    {
        var date = endsOn.ToDateTime(TimeOnly.MinValue);
        return frequency switch
        {
            ContributionFrequency.Weekly => $"{ISOWeek.GetYear(date)}-W{ISOWeek.GetWeekOfYear(date):00}",
            ContributionFrequency.Fortnightly => endsOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => endsOn.ToString("yyyy-MM", CultureInfo.InvariantCulture)
        };
    }

    private static bool IsLastDayOfMonth(DateOnly d) => d.Day == DateTime.DaysInMonth(d.Year, d.Month);
    private static DateOnly LastDayOfMonth(DateOnly d) => new(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month));
}