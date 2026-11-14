namespace Sentium.Tempus.Common;

/// <summary>
/// When a Reminder fires. The offset is in minutes before the Entry: an Entry with a time fires that long before
/// it; an Entry with only a day fires at the user's day-start on the day, the day before or a week before (whole
/// days of the offset); a month-pinned Entry fires at day-start on the first of its month.
/// </summary>
internal static class ReminderSchedule
{
    public const int OnTheDay = 0;
    public const int DayBefore = 24 * 60;
    public const int WeekBefore = 7 * DayBefore;

    public static DateTimeOffset DueAt(DateOnly? date, TimeOnly? startTime, int year, int month, int offsetMinutes, TimeZoneInfo zone, TimeOnly dayStart)
    {
        ArgumentNullException.ThrowIfNull(zone);

        if (date is not { } day)
        {
            return ToUtc(TimelineCalendar.FirstDay(year, month).ToDateTime(dayStart), zone);
        }

        if (startTime is { } time)
        {
            return ToUtc(day.ToDateTime(time), zone).AddMinutes(-offsetMinutes);
        }

        return ToUtc(day.AddDays(-(offsetMinutes / DayBefore)).ToDateTime(dayStart), zone);
    }

    /// <summary>
    /// A local time skipped by a clock change is read with the offset before the change, which lands it just past
    /// the gap (03:30 in a skipped 03:00-04:00 fires at 04:30), so the Reminder still fires.
    /// </summary>
    private static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo zone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (!zone.IsInvalidTime(unspecified))
        {
            return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, zone));
        }

        var offsetBefore = zone.GetUtcOffset(unspecified.AddDays(-1));
        return new DateTimeOffset(DateTime.SpecifyKind(unspecified - offsetBefore, DateTimeKind.Utc));
    }
}
