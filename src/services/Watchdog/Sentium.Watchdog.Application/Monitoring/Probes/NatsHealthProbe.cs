using System.Diagnostics;
using NATS.Client.Core;
using Sentium.Watchdog.Core.Monitoring;

namespace Sentium.Watchdog.Application.Monitoring.Probes;

public sealed class NatsHealthProbe(INatsConnection connection) : IHealthProbe
{
    public string Target => "NATS";
    public ComponentKind Kind => ComponentKind.Infrastructure;

    public async Task<ServiceHealthStatus> ProbeAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var rtt = await connection.PingAsync(ct);
            sw.Stop();
            return ProbeResult.Create(Target, Kind, ServiceStatus.Healthy, rtt.TotalMilliseconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            sw.Stop();
            return ProbeResult.Create(Target, Kind, ServiceStatus.Unhealthy, sw.Elapsed.TotalMilliseconds, "Probe timed out");
        }
        catch (Exception ex)
        {
            sw.Stop();
            var state = connection.ConnectionState;
            var status = state is NatsConnectionState.Connecting or NatsConnectionState.Reconnecting ? ServiceStatus.Degraded : ServiceStatus.Unhealthy;
            return ProbeResult.Create(Target, Kind, status, sw.Elapsed.TotalMilliseconds, $"{state}: {ex.Message}");
        }
    }
}
