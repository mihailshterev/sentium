using Sentium.Core.Constants;
using Sentium.Registry.Contracts;
using Sentium.Registry.Contracts.Settings;

namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// The zone a Schedule's cron is read in: the owner's own calendar zone, else the household's, else UTC. It is captured
/// when the Schedule is saved; a later change of zone applies the next time the Schedule is edited.
/// </summary>
internal sealed class OwnerTimeZoneResolver(ISettingsService settings, ILogger<OwnerTimeZoneResolver> logger)
{
    public async Task<TimeZoneInfo> ResolveAsync(Guid ownerId, CancellationToken ct)
    {
        try
        {
            var own = await settings.GetValueAsync<UserCalendarSettings>(SettingsKeys.UserCalendar, ownerId, ct);
            var household = own?.TimeZone is null ? await settings.GetValueAsync<CalendarSettings>(SettingsKeys.Calendar, null, ct) : null;
            var zoneId = own?.TimeZone ?? household?.TimeZone;

            return zoneId is not null && TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out var zone) ? zone : TimeZoneInfo.Utc;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not read the calendar settings of {UserId}; scheduling in UTC", ownerId);
            return TimeZoneInfo.Utc;
        }
    }
}
