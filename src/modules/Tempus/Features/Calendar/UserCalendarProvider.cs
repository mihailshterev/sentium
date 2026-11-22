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
            var calendar = await settings.GetEffectiveCalendarAsync(userId, ct);
            return new UserCalendar(calendar.TimeZone, calendar.DayStart);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not read the calendar settings of {UserId}; using UTC", userId);
            return UserCalendar.Utc;
        }
    }
}
