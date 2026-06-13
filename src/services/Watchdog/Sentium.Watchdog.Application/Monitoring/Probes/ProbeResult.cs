using Sentium.Watchdog.Core.Monitoring;

namespace Sentium.Watchdog.Application.Monitoring.Probes;

internal static class ProbeResult
{
    public static ServiceHealthStatus Create(
        string target,
        ComponentKind kind,
        ServiceStatus status,
        double latencyMs,
        string? details = null,
        IReadOnlyList<HealthCheckEntry>? checks = null)
        => new()
        {
            ServiceName = target,
            Kind = kind,
            Status = status,
            LatencyMs = Math.Round(latencyMs, 1),
            CheckedAt = DateTimeOffset.UtcNow,
            Details = details,
            Checks = checks ?? []
        };
}
