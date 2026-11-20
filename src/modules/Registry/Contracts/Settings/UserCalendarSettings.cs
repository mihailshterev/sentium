using Sentium.Core.Constants;

namespace Sentium.Registry.Contracts.Settings;

/// <summary>
/// A user's own calendar settings; a <see langword="null"/> field falls back to the household's
/// <see cref="CalendarSettings"/>.
/// </summary>
[SettingsGroup(
    SettingsKeys.UserCalendar,
    SettingsScope.Personal,
    "calendar",
    "Calendar",
    "When your day turns over: events count as past after your midnight, and reminders for entries without a time fire at your day start.",
    Order = 1)]
public sealed class UserCalendarSettings
{
    [Setting(
        "Time zone",
        "Your time zone for the timeline and reminders.",
        Keywords = ["timezone", "tz", "region", "clock"],
        Control = SettingControlKind.TimeZone)]
    public string? TimeZone { get; set; }

    [Setting("Day starts at", "Leave empty to use the household's.", Keywords = ["morning", "reminder", "time"])]
    public TimeOnly? DayStart { get; set; }
}
