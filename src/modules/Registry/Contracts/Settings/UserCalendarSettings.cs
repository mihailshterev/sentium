namespace Sentium.Registry.Contracts.Settings;

/// <summary>
/// A user's own calendar settings; a <see langword="null"/> field falls back to the household's
/// <see cref="CalendarSettings"/>.
/// </summary>
public sealed class UserCalendarSettings
{
    public string? TimeZone { get; set; }

    public TimeOnly? DayStart { get; set; }
}
