namespace Sentium.Vigil.Features.Devices;

/// <summary>
/// Decides when a Device's nearly full disk is worth telling its owner about: once it has been above the threshold
/// for several Reports in a row, and then not again until the disk has dropped back under it. One instance tracks
/// one Device; it is not thread-safe.
/// </summary>
internal sealed class DiskFullRule
{
    public const double ThresholdPercent = 90;
    public const int ConsecutiveReports = 3;

    private readonly Dictionary<string, Streak> _streaks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Feeds the disks of one Report and returns those that have just crossed into "tell the owner".
    /// </summary>
    public IReadOnlyList<DeviceDiskReport> Evaluate(IReadOnlyList<DeviceDiskReport> disks)
    {
        ArgumentNullException.ThrowIfNull(disks);

        ForgetMissingDisks(disks);

        List<DeviceDiskReport> newlyFull = [];
        foreach (var disk in disks)
        {
            if (disk.UsedPercent <= ThresholdPercent)
            {
                _streaks.Remove(disk.Name);
                continue;
            }

            var streak = _streaks.GetValueOrDefault(disk.Name);
            streak = streak with { Reports = streak.Reports + 1 };

            if (streak.Reports >= ConsecutiveReports && !streak.Notified)
            {
                streak = streak with { Notified = true };
                newlyFull.Add(disk);
            }

            _streaks[disk.Name] = streak;
        }

        return newlyFull;
    }

    private void ForgetMissingDisks(IReadOnlyList<DeviceDiskReport> disks)
    {
        var missing = _streaks.Keys
            .Where(name => !disks.Any(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var name in missing)
        {
            _streaks.Remove(name);
        }
    }

    private readonly record struct Streak(int Reports, bool Notified);
}
