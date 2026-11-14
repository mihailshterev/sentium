namespace Sentium.Registry.Features.Settings;

/// <summary>
/// The household's calendar defaults, used by every user who has not set their own (see
/// <see cref="UserCalendarSettings"/>).
/// </summary>
internal sealed class CalendarSettings
{
    public const string DefaultTimeZone = "UTC";

    public static readonly TimeOnly DefaultDayStart = new(8, 0);

    /// <summary>
    /// An IANA time zone id, for example <c>Europe/Sofia</c>.
    /// </summary>
    public string TimeZone { get; set; } = DefaultTimeZone;

    /// <summary>
    /// When the day starts: Reminders for Entries without a time fire then.
    /// </summary>
    public TimeOnly DayStart { get; set; } = DefaultDayStart;
}
