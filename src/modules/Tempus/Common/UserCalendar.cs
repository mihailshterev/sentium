namespace Sentium.Tempus.Common;

/// <summary>
/// The time zone and day-start a user's timeline is read in: "today", "past" and Reminder times all depend on it.
/// </summary>
internal sealed record UserCalendar(TimeZoneInfo Zone, TimeOnly DayStart)
{
    public static readonly TimeOnly DefaultDayStart = new(8, 0);

    public static UserCalendar Utc { get; } = new(TimeZoneInfo.Utc, DefaultDayStart);

    public DateOnly Today(DateTimeOffset now) => TimelineCalendar.TodayIn(Zone, now);

    public DateTimeOffset? ReminderDueAt(DateOnly? date, TimeOnly? startTime, int year, int month, int? offsetMinutes)
    {
        return offsetMinutes is { } offset
            ? ReminderSchedule.DueAt(date, startTime, year, month, offset, Zone, DayStart)
            : null;
    }
}
