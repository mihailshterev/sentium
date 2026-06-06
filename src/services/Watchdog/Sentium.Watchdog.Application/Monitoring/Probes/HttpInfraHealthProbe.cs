using System.Diagnostics;
using Sentium.Watchdog.Core.Monitoring;

namespace Sentium.Watchdog.Application.Monitoring.Probes;

public sealed class HttpInfraHealthProbe(IHttpClientFactory httpClientFactory, string clientName, string target, string healthPath) : IHealthProbe
{
    public string Target => target;
    public ComponentKind Kind => ComponentKind.Infrastructure;

    public async Task<ServiceHealthStatus> ProbeAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var client = httpClientFactory.CreateClient(clientName);
            using var response = await client.GetAsync(healthPath, ct);
            sw.Stop();

            var status = response.IsSuccessStatusCode ? ServiceStatus.Healthy : ServiceStatus.Unhealthy;
            var details = response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
            return ProbeResult.Create(target, Kind, status, sw.Elapsed.TotalMilliseconds, details);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            sw.Stop();
            return ProbeResult.Create(target, Kind, ServiceStatus.Unhealthy, sw.Elapsed.TotalMilliseconds, "Probe timed out");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ProbeResult.Create(target, Kind, ServiceStatus.Unhealthy, sw.Elapsed.TotalMilliseconds, ex.Message);
        }
    }
}
