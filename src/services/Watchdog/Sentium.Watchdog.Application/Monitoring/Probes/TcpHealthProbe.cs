using System.Diagnostics;
using System.Net.Sockets;
using Sentium.Watchdog.Core.Monitoring;

namespace Sentium.Watchdog.Application.Monitoring.Probes;

public sealed class TcpHealthProbe(string target, string host, int port) : IHealthProbe
{
    public string Target => target;
    public ComponentKind Kind => ComponentKind.Infrastructure;

    public async Task<ServiceHealthStatus> ProbeAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port, ct);
            sw.Stop();
            return ProbeResult.Create(target, Kind, ServiceStatus.Healthy, sw.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            sw.Stop();
            return ProbeResult.Create(target, Kind, ServiceStatus.Unhealthy, sw.Elapsed.TotalMilliseconds, $"Connection to {host}:{port} timed out");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ProbeResult.Create(target, Kind, ServiceStatus.Unhealthy, sw.Elapsed.TotalMilliseconds, ex.Message);
        }
    }
}
