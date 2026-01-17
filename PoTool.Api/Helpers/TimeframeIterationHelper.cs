using System.Globalization;

namespace PoTool.Api.Helpers;

/// <summary>
/// Helper for assigning PRs to timeframe iterations (weekly buckets using ISO 8601 week numbering).
/// </summary>
public static class TimeframeIterationHelper
{
    /// <summary>
    /// Gets the ISO week number and year for a given date.
    /// Uses ISO 8601 week date system: weeks start on Monday, and week 1 is the week with the first Thursday of the year.
    /// </summary>
    public static (int Year, int WeekNumber) GetIsoWeek(DateTimeOffset date)
    {
        var dateTime = date.UtcDateTime;
        var calendar = CultureInfo.InvariantCulture.Calendar;
        var rule = CalendarWeekRule.FirstFourDayWeek;
        var firstDayOfWeek = DayOfWeek.Monday;

        var weekNumber = calendar.GetWeekOfYear(dateTime, rule, firstDayOfWeek);
        var year = dateTime.Year;

        // Handle edge case: Week 1 in December belongs to next year
        if (weekNumber == 1 && dateTime.Month == 12)
        {
            year++;
        }
        // Handle edge case: Week 52/53 in January belongs to previous year
        else if (weekNumber >= 52 && dateTime.Month == 1)
        {
            year--;
        }

        return (year, weekNumber);
    }

    /// <summary>
    /// Gets the iteration key for a given date (format: "YYYY-Wnn").
    /// </summary>
    public static string GetIterationKey(DateTimeOffset date)
    {
        var (year, weekNumber) = GetIsoWeek(date);
        return $"{year}-W{weekNumber:D2}";
    }

    /// <summary>
    /// Gets the start of the ISO week (Monday at 00:00:00 UTC) for a given date.
    /// </summary>
    public static DateTimeOffset GetWeekStart(DateTimeOffset date)
    {
        var dateTime = date.UtcDateTime;
        
        // Find Monday of this week
        var daysFromMonday = ((int)dateTime.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var monday = dateTime.Date.AddDays(-daysFromMonday);
        
        return new DateTimeOffset(monday, TimeSpan.Zero);
    }

    /// <summary>
    /// Gets the end of the ISO week (Sunday at 23:59:59 UTC) for a given date.
    /// </summary>
    public static DateTimeOffset GetWeekEnd(DateTimeOffset date)
    {
        var weekStart = GetWeekStart(date);
        var sunday = weekStart.AddDays(7).AddTicks(-1); // End of Sunday
        return sunday;
    }
}
