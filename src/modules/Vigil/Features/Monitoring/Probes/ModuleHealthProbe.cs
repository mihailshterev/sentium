using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Sentium.Vigil.Features.Monitoring.Probes;

/// <summary>
/// Probes a module hosted in this process by running the health checks the module tagged with its
/// name (its DbContext and so on), instead of calling a remote <c>/health</c> endpoint.
/// </summary>
public sealed class ModuleHealthProbe(HealthCheckService healthChecks, string target, string tag) : IHealthProbe
{
    public string Target => target;
    public ComponentKind Kind => ComponentKind.Service;

    public async Task<ServiceHealthStatus> ProbeAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var report = await healthChecks.CheckHealthAsync(registration => registration.Tags.Contains(tag), ct);
            sw.Stop();

            var checks = report.Entries
                .Select(e => new HealthCheckEntry(e.Key, e.Value.Status.ToString(), e.Value.Description ?? e.Value.Exception?.Message, e.Value.Duration.TotalMilliseconds))
                .ToList();

            var status = report.Status switch
            {
                HealthStatus.Healthy => ServiceStatus.Healthy,
                HealthStatus.Degraded => ServiceStatus.Degraded,
                _ => ServiceStatus.Unhealthy
            };

            var failing = checks.Where(c => c.Status != nameof(HealthStatus.Healthy)).Select(c => c.Name).ToList();
            var details = failing.Count == 0 ? null : $"Failing checks: {string.Join(", ", failing)}";

            return ProbeResult.Create(target, Kind, status, sw.Elapsed.TotalMilliseconds, details, checks);
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
