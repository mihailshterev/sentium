using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sentium.Tempus.Common;
using Sentium.Tempus.Data;
using Sentium.Tempus.Features.Calendar;

namespace Sentium.Tempus.Features.Series;

/// <summary>
/// Extends every Series to its rolling horizon. Runs in a system scope across every user; each Series' own user id
/// picks the calendar its occurrences' Reminders are computed in. A Series a concurrent run already extended fails
/// on the unique occurrence index and is simply picked up again next time.
/// </summary>
internal sealed class TimelineSeriesMaterializer(
    TempusDbContext db,
    TimelineSeriesWriter writer,
    IUserCalendarProvider calendars,
    ILogger<TimelineSeriesMaterializer> logger)
{
    private const string UniqueViolation = "23505";

    /// <returns>The number of occurrences added.</returns>
    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        // A day of slack: a user's zone may be a day ahead of UTC, and each Series is extended in its own zone.
        var horizonFloor = UserCalendar.Utc.Today(now).AddMonths(TempusLimits.SeriesHorizonMonths).AddDays(-1);
        var seriesIds = await db.TimelineSeries
            .AsNoTracking()
            .Where(s => s.MaterializedThrough == null
                || (s.MaterializedThrough < horizonFloor && (s.Until == null || s.Until > s.MaterializedThrough)))
            .Select(s => new { s.Id, s.UserId })
            .ToListAsync(ct);

        var added = 0;
        foreach (var user in seriesIds.GroupBy(s => s.UserId))
        {
            var calendar = await calendars.GetAsync(user.Key, ct);
            foreach (var entry in user)
            {
                added += await ExtendAsync(entry.Id, calendar, now, ct);
            }
        }

        return added;
    }

    private async Task<int> ExtendAsync(Guid seriesId, UserCalendar calendar, DateTimeOffset now, CancellationToken ct)
    {
        db.ChangeTracker.Clear();

        var series = await db.TimelineSeries.FirstOrDefaultAsync(s => s.Id == seriesId, ct);
        if (series is null)
        {
            return 0;
        }

        try
        {
            return await writer.MaterializeAsync(series, calendar, now, ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            logger.LogInformation("Series {SeriesId} was extended concurrently; skipping it this run", seriesId);
            return 0;
        }
    }
}
