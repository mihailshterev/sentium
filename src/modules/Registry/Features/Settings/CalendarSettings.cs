namespace Sentium.Registry.Features.Settings;

/// <summary>
/// The household's calendar defaults, used by every user who has not set their own (see
/// <see cref="UserCalendarSettings"/>).
/// </summary>
internal sealed class CalendarSettings
{
    /// <summary>
    /// An IANA time zone id, for example <c>Europe/Sofia</c>.
    /// </summary>
    public string TimeZone { get; set; } = "UTC";

    /// <summary>
    /// When the day starts: Reminders for Entries without a time fire then.
    /// </summary>
    public TimeOnly DayStart { get; set; } = new(8, 0);
}
