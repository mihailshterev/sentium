using System.Globalization;
using Sentium.Vigil.Features.Monitoring;

namespace Sentium.Vigil.Features.Backups;

/// <summary>
/// Turns the sidecar's status into a health verdict. Backups are unhealthy when the destination is not mounted,
/// the last run failed, or the newest verified backup is older than the limit. A sidecar that has never made a
/// backup gets the same limit from when it started, so a fresh installation does not alarm before its first night.
/// </summary>
internal static class BackupHealthEvaluator
{
    public static BackupHealth Evaluate(BackupStatusDto status, DateTimeOffset now, TimeSpan maxAge)
    {
        ArgumentNullException.ThrowIfNull(status);

        if (!status.DestinationReady)
        {
            return Unhealthy(status.DestinationProblem ?? "The backup destination is not available.");
        }

        if (status.LastRun is { Succeeded: false } failed)
        {
            return Unhealthy($"The last backup failed: {failed.Error}");
        }

        var newest = status.Backups.MaxBy(b => b.CreatedAt);
        if (newest is null)
        {
            return now - status.ServiceStartedAt <= maxAge
                ? new BackupHealth(ServiceStatus.Healthy, "No backup yet.")
                : Unhealthy(Invariant($"No backup in the last {maxAge.TotalHours:0} hours."));
        }

        var age = now - newest.CreatedAt;
        return age > maxAge
            ? Unhealthy(Invariant($"The newest backup is {age.TotalHours:0} hours old (limit {maxAge.TotalHours:0})."))
            : new BackupHealth(ServiceStatus.Healthy, null);
    }

    private static BackupHealth Unhealthy(string details) => new(ServiceStatus.Unhealthy, details);

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
