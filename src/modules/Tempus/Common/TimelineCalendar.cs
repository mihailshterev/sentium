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
