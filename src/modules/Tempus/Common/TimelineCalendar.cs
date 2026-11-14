using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Common;

/// <summary>
/// Calendar arithmetic shared by the services and the agent facade.
/// </summary>
internal static class TimelineCalendar
{
    public static DateOnly FirstDay(int year, int month) => new(year, month, 1);

    public static DateOnly LastDay(int year, int month) => new(year, month, DateTime.DaysInMonth(year, month));

    /// <summary>
    /// Months since year 0, so month ranges compare as integers.
    /// </summary>
    public static int MonthIndex(int year, int month) => (year * 12) + (month - 1);

    public static bool IsInMonth(DateOnly date, int year, int month) => date.Year == year && date.Month == month;

    /// <summary>
    /// The date a task must be done by: its due date, else its planned day, else the end of its month.
    /// </summary>
    public static DateOnly EffectiveDeadline(DateOnly? dueDate, DateOnly? date, int year, int month)
        => dueDate ?? date ?? LastDay(year, month);

    public static bool IsOverdue(TimelineTaskStatus status, DateOnly? dueDate, DateOnly? date, int year, int month, DateOnly today)
        => status != TimelineTaskStatus.Done && EffectiveDeadline(dueDate, date, year, month) < today;

    /// <summary>
    /// A Calendar Event is past once its last day (the end day, else its day, else its month's end) is before
    /// <paramref name="today"/>. Derived on every read, never stored.
    /// </summary>
    public static bool IsPast(DateOnly? date, DateOnly? endDate, int year, int month, DateOnly today)
        => (endDate ?? date ?? LastDay(year, month)) < today;

    /// <summary>
    /// The days from <paramref name="start"/> through <paramref name="end"/>, clipped to the window
    /// <paramref name="from"/>..<paramref name="to"/>.
    /// </summary>
    public static IEnumerable<DateOnly> DaysBetween(DateOnly start, DateOnly end, DateOnly from, DateOnly to)
    {
        var first = start > from ? start : from;
        var last = end < to ? end : to;
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            yield return day;
        }
    }

    public static DateOnly TodayIn(TimeZoneInfo zone, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
    }

    public static DateOnly UtcToday(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
    }

    /// <summary>
    /// The client's local date when it is within a day of the server's UTC date (every real time zone is), else
    /// the UTC date - so a wrong client clock cannot skew the overdue counts.
    /// </summary>
    public static DateOnly ResolveToday(DateOnly? clientToday, DateOnly utcToday)
        => clientToday is { } client && Math.Abs(client.DayNumber - utcToday.DayNumber) <= 1 ? client : utcToday;
}
