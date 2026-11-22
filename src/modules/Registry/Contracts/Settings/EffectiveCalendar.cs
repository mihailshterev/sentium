namespace Sentium.Registry.Contracts.Settings;

/// <summary>
/// A user's calendar after fallbacks: each field is the user's own <see cref="UserCalendarSettings"/> value, else the
/// household's <see cref="CalendarSettings"/>, else UTC and <see cref="CalendarSettings.DefaultDayStart"/>.
/// </summary>
public sealed record EffectiveCalendar(TimeZoneInfo TimeZone, TimeOnly DayStart);
