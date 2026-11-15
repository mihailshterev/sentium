using System.Diagnostics;
using Microsoft.Extensions.Options;
using Sentium.Nuntius.Contracts;
using Sentium.Vigil.Features.Monitoring;
using Sentium.Vigil.Features.Monitoring.Probes;

namespace Sentium.Vigil.Features.Backups;

/// <summary>
/// Reads the sidecar's status every poll. An unhealthy verdict opens an incident through the usual flow, which
/// notifies the Sovereigns under <c>system.backup</c>.
/// </summary>
internal sealed class BackupHealthProbe(
    IBackupClient client,
    IOptions<BackupMonitorOptions> options,
    TimeProvider time) : IIncidentCategoryProbe
{
    public const string TargetName = "Backup";

    public string Target => TargetName;
    public ComponentKind Kind => ComponentKind.Infrastructure;
    public string NotificationCategory => NotificationCategories.SystemBackup;

    public async Task<ServiceHealthStatus> ProbeAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var status = await client.GetStatusAsync(ct);
            sw.Stop();

            if (status is null)
            {
                return ProbeResult.Create(Target, Kind, ServiceStatus.Unhealthy, sw.Elapsed.TotalMilliseconds, "The backup service is unreachable.");
            }

            var health = BackupHealthEvaluator.Evaluate(status, time.GetUtcNow(), options.Value.MaxAge);
            return ProbeResult.Create(Target, Kind, health.Status, sw.Elapsed.TotalMilliseconds, health.Details);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            sw.Stop();
            return ProbeResult.Create(Target, Kind, ServiceStatus.Unhealthy, sw.Elapsed.TotalMilliseconds, "Probe timed out");
        }
    }
}
