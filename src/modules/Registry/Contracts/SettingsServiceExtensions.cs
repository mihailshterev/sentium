using System.Text.Json;
using Sentium.Core.Constants;
using Sentium.Registry.Contracts.Settings;

namespace Sentium.Registry.Contracts;

public static class SettingsServiceExtensions
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Reads the settings for <paramref name="key"/> and deserializes the value into the caller's own
    /// shape, or returns <c>null</c> when the key is unknown.
    /// </summary>
    public static async Task<T?> GetValueAsync<T>(this ISettingsService settings, string key, Guid? userId = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var envelope = await settings.GetAsync(key, userId, ct);
        return envelope is null ? default : envelope.Value.Deserialize<T>(WebOptions);
    }

    /// <summary>
    /// The calendar a user's dates are read in. Without a user (<see langword="null"/> or <see cref="Guid.Empty"/>)
    /// only the household's settings apply. A zone id this host does not know resolves to UTC rather than to the next
    /// fallback. Read failures propagate, so each caller decides how to log and degrade.
    /// </summary>
    public static async Task<EffectiveCalendar> GetEffectiveCalendarAsync(this ISettingsService settings, Guid? userId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var own = userId is { } id && id != Guid.Empty
            ? await settings.GetValueAsync<UserCalendarSettings>(SettingsKeys.UserCalendar, id, ct)
            : null;

        // Registry caches every envelope, but skip the household read when the user's own settings already answer both.
        var household = own?.TimeZone is null || own.DayStart is null
            ? await settings.GetValueAsync<CalendarSettings>(SettingsKeys.Calendar, null, ct)
            : null;

        var zoneId = own?.TimeZone ?? household?.TimeZone;
        var zone = zoneId is not null && TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out var found) ? found : TimeZoneInfo.Utc;

        return new EffectiveCalendar(zone, own?.DayStart ?? household?.DayStart ?? CalendarSettings.DefaultDayStart);
    }
}
