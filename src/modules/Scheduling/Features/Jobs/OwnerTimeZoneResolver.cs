using Sentium.Registry.Contracts;

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
            return (await settings.GetEffectiveCalendarAsync(ownerId, ct)).TimeZone;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not read the calendar settings of {UserId}; scheduling in UTC", ownerId);
            return TimeZoneInfo.Utc;
        }
    }
}
