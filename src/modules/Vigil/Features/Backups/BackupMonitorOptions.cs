using System.ComponentModel.DataAnnotations;

namespace Sentium.Vigil.Features.Backups;

/// <summary>
/// Bound from the <c>Backup</c> configuration section. The backup sidecar only runs under Compose; without an
/// endpoint (Aspire development) there is no probe and the portal's Backups panel says backups are not set up.
/// </summary>
internal sealed class BackupMonitorOptions
{
    public const string SectionName = "Backup";

    /// <summary>
    /// Root URL of the backup sidecar. Empty switches backup monitoring off.
    /// </summary>
    public Uri? Endpoint { get; set; }

    /// <summary>
    /// Age of the newest verified backup beyond which Vigil reports backups unhealthy.
    /// </summary>
    [Range(1, 24 * 365)]
    public int MaxAgeHours { get; set; } = 36;

    public bool IsConfigured => Endpoint is { IsAbsoluteUri: true, Scheme: "http" or "https" };

    public TimeSpan MaxAge => TimeSpan.FromHours(MaxAgeHours);
}
