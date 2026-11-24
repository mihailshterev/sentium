using System.Globalization;
using Microsoft.Extensions.Options;
using Sentium.Vigil.Features.Metrics;

namespace Sentium.Vigil.Features.Monitoring.Probes;

/// <summary>
/// Watches the free space of the disks Sentium.Web can see. In a container that is the disk holding Docker's data,
/// which the database, object storage and container logs share unless they are mounted elsewhere; a full disk stops
/// the database and every upload. The fullest disk sets the status: degraded from
/// <see cref="DiskSpaceOptions.WarningPercent"/>, unhealthy from <see cref="DiskSpaceOptions.CriticalPercent"/>.
/// </summary>
internal sealed class DiskSpaceProbe(Func<IReadOnlyList<DiskInfo>> readDisks, IOptions<DiskSpaceOptions> options) : IHealthProbe
{
    public const string TargetName = "Disk space";

    public string Target => TargetName;

    public ComponentKind Kind => ComponentKind.Infrastructure;

    public Task<ServiceHealthStatus> ProbeAsync(CancellationToken ct)
    {
        var disks = readDisks().Where(d => d.TotalGb > 0).ToList();
        if (disks.Count == 0)
        {
            return Task.FromResult(ProbeResult.Create(Target, Kind, ServiceStatus.Healthy, 0, "No disks are visible to the server."));
        }

        var fullest = disks.MaxBy(d => d.UsagePercent)!;
        var thresholds = options.Value;
        var status = fullest.UsagePercent >= thresholds.CriticalPercent ? ServiceStatus.Unhealthy
            : fullest.UsagePercent >= thresholds.WarningPercent ? ServiceStatus.Degraded
            : ServiceStatus.Healthy;

        var details = string.Create(
            CultureInfo.InvariantCulture,
            $"{fullest.Name} is {fullest.UsagePercent:0}% full ({fullest.AvailableGb:0.#} GB free of {fullest.TotalGb:0.#} GB).");

        return Task.FromResult(ProbeResult.Create(Target, Kind, status, 0, details));
    }
}
