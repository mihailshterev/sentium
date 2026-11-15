namespace Sentium.Backup.Running;

internal static class BackupSchedule
{
    /// <summary>
    /// The next moment after <paramref name="now"/> at which the local clock in <paramref name="zone"/> reads
    /// <paramref name="timeOfDay"/>. A time skipped by a daylight-saving jump moves forward by the size of the jump.
    /// </summary>
    public static DateTimeOffset NextOccurrence(DateTimeOffset now, TimeOnly timeOfDay, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var candidate = DateOnly.FromDateTime(localNow.DateTime).ToDateTime(timeOfDay);
        if (candidate <= localNow.DateTime)
        {
            candidate = candidate.AddDays(1);
        }

        if (zone.IsInvalidTime(candidate))
        {
            candidate = candidate.AddHours(1);
        }

        return new DateTimeOffset(candidate, zone.GetUtcOffset(candidate));
    }
}
