using System.ComponentModel.DataAnnotations;

namespace Sentium.Vigil.Features.Monitoring.Probes;

/// <summary>
/// Bound from <c>Vigil:Disk</c>. Percentages of a disk's capacity in use.
/// </summary>
internal sealed class DiskSpaceOptions
{
    public const string SectionName = "Vigil:Disk";

    [Range(1, 100)]
    public double WarningPercent { get; set; } = 90;

    [Range(1, 100)]
    public double CriticalPercent { get; set; } = 95;
}
