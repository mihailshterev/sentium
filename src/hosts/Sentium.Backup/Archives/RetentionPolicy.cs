using System.Globalization;

namespace Sentium.Backup.Archives;

/// <summary>
/// Grandfather-father-son selection over UTC calendar days and ISO weeks: the newest archive of each of the
/// most recent <c>keepDaily</c> days and of each of the most recent <c>keepWeekly</c> weeks survives. The
/// newest archive always survives, whatever the settings.
/// </summary>
internal static class RetentionPolicy
{
    public static IReadOnlyList<BackupArchive> SelectForDeletion(IEnumerable<BackupArchive> archives, int keepDaily, int keepWeekly)
    {
        ArgumentNullException.ThrowIfNull(archives);

        var newestFirst = archives.OrderByDescending(a => a.CreatedAt).ToList();
        if (newestFirst.Count == 0)
        {
            return [];
        }

        var keep = new HashSet<BackupArchive> { newestFirst[0] };
        keep.UnionWith(NewestPerPeriod(newestFirst, keepDaily, a => DateOnly.FromDateTime(a.CreatedAt.UtcDateTime)));
        keep.UnionWith(NewestPerPeriod(newestFirst, keepWeekly, IsoWeekOf));

        return [.. newestFirst.Where(a => !keep.Contains(a))];
    }

    private static IEnumerable<BackupArchive> NewestPerPeriod<TKey>(
        List<BackupArchive> newestFirst, int periods, Func<BackupArchive, TKey> periodOf)
        where TKey : notnull
    {
        var seen = new HashSet<TKey>();
        foreach (var archive in newestFirst)
        {
            if (seen.Count >= periods)
            {
                yield break;
            }

            if (seen.Add(periodOf(archive)))
            {
                yield return archive;
            }
        }
    }

    private static (int Year, int Week) IsoWeekOf(BackupArchive archive)
    {
        var date = archive.CreatedAt.UtcDateTime;
        return (ISOWeek.GetYear(date), ISOWeek.GetWeekOfYear(date));
    }
}
