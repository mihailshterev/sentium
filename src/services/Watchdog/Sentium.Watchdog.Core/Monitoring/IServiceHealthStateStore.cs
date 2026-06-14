namespace Sentium.Watchdog.Core.Monitoring;

/// <summary>
/// In-memory store of the latest health status and rolling sample history per target.
/// </summary>
public interface IServiceHealthStateStore
{
    /// <summary>
    /// Records the latest probe result and returns the enriched status (with uptime, last state
    /// change, and consecutive-failure count computed from the rolling history).
    /// </summary>
    ServiceHealthStatus UpdateStatus(ServiceHealthStatus status);

    IReadOnlyList<ServiceHealthStatus> GetAll();

    ServiceHealthStatus? Get(string serviceName);

    /// <summary>
    /// Returns up to <paramref name="take"/> most-recent samples for a target, oldest first.
    /// </summary>
    IReadOnlyList<HealthSample> GetSamples(string serviceName, int take);
}
