namespace Sentium.Watchdog.Core.Monitoring;

public interface IHealthProbe
{
    string Target { get; }

    ComponentKind Kind { get; }

    Task<ServiceHealthStatus> ProbeAsync(CancellationToken ct);
}
