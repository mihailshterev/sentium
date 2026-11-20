using Sentium.Core.Constants;

namespace Sentium.Registry.Contracts.Settings;

/// <summary>
/// The household's calendar defaults, used by every user who has not set their own (see
/// <see cref="UserCalendarSettings"/>).
/// </summary>
[SettingsGroup(
    SettingsKeys.Calendar,
    SettingsScope.Household,
    "household-calendar",
    "Household calendar",
    "Defaults for everyone who hasn't set their own time zone or day start.",
    Order = 1)]
public sealed class CalendarSettings
{
    public const string DefaultTimeZone = "UTC";

    public static readonly TimeOnly DefaultDayStart = new(8, 0);

    /// <summary>
    /// An IANA time zone id, for example <c>Europe/Sofia</c>.
    /// </summary>
    [Setting(
        "Time zone",
        "The household's time zone.",
        Keywords = ["timezone", "tz", "region", "clock"],
        Control = SettingControlKind.TimeZone)]
    public string TimeZone { get; set; } = DefaultTimeZone;

    /// <summary>
    /// When the day starts: Reminders for Entries without a time fire then.
    /// </summary>
    [Setting("Day starts at", "When reminders for entries without a time fire.", Keywords = ["morning", "reminder", "time"])]
    public TimeOnly DayStart { get; set; } = DefaultDayStart;
}
