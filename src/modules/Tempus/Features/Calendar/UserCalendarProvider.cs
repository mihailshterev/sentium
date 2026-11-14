using Sentium.Core.Constants;
using Sentium.Registry.Contracts;
using Sentium.Tempus.Common;

namespace Sentium.Tempus.Features.Calendar;

/// <summary>
/// Reads the calendar settings through Registry's cache on every call; nothing is cached here.
/// </summary>
internal sealed class UserCalendarProvider(ISettingsService settings, ILogger<UserCalendarProvider> logger) : IUserCalendarProvider
{
    public async Task<UserCalendar> GetAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var household = await settings.GetValueAsync<CalendarValue>(SettingsKeys.Calendar, null, ct);
            var own = userId == Guid.Empty ? null : await settings.GetValueAsync<CalendarValue>(SettingsKeys.UserCalendar, userId, ct);

            var zoneId = own?.TimeZone ?? household?.TimeZone;
            var zone = zoneId is not null && TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out var found) ? found : TimeZoneInfo.Utc;

            return new UserCalendar(zone, own?.DayStart ?? household?.DayStart ?? UserCalendar.DefaultDayStart);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not read the calendar settings of {UserId}; using UTC", userId);
            return UserCalendar.Utc;
        }
    }

    private sealed record CalendarValue(string? TimeZone, TimeOnly? DayStart);
}
