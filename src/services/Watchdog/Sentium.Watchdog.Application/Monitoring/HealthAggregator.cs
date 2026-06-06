using Sentium.Watchdog.Core.Monitoring;

namespace Sentium.Watchdog.Application.Monitoring;

/// <summary>
/// Rolls a set of per-target statuses up into a single <see cref="SystemHealthOverview"/>.
/// </summary>
public static class HealthAggregator
{
    public static SystemHealthOverview BuildOverview(IReadOnlyList<ServiceHealthStatus> statuses, int openIncidents)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        var healthy = statuses.Count(s => s.Status == ServiceStatus.Healthy);
        var degraded = statuses.Count(s => s.Status == ServiceStatus.Degraded);
        var unhealthy = statuses.Count(s => s.Status == ServiceStatus.Unhealthy);
        var unknown = statuses.Count(s => s.Status == ServiceStatus.Unknown);

        var overall = statuses.Count == 0
            ? ServiceStatus.Unknown
            : unhealthy > 0
                ? ServiceStatus.Unhealthy
                : degraded > 0
                    ? ServiceStatus.Degraded
                    : healthy == statuses.Count
                        ? ServiceStatus.Healthy
                        : ServiceStatus.Unknown;

        return new SystemHealthOverview
        {
            Total = statuses.Count,
            Healthy = healthy,
            Degraded = degraded,
            Unhealthy = unhealthy,
            Unknown = unknown,
            OverallStatus = overall,
            OpenIncidents = openIncidents,
            GeneratedAt = DateTimeOffset.UtcNow
        };
    }
}
