namespace Sentium.Watchdog.Core.Monitoring;

/// <summary>
/// A single health check against one monitored target (service or infrastructure component).
/// </summary>
public interface IHealthProbe
{
    string Target { get; }

    ComponentKind Kind { get; }

    /// <summary>
    /// Runs the probe and returns the observed health status.
    /// </summary>
    Task<ServiceHealthStatus> ProbeAsync(CancellationToken ct);
}
