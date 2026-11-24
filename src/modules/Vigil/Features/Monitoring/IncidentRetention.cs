using Microsoft.EntityFrameworkCore;
using Sentium.Infrastructure.Retention;
using Sentium.Vigil.Data;

namespace Sentium.Vigil.Features.Monitoring;

/// <summary>
/// Deletes resolved incidents older than <c>Vigil:IncidentRetentionDays</c> (default 365). Open incidents stay.
/// </summary>
internal sealed class IncidentRetention(VigilDbContext db, IConfiguration configuration, ILogger<IncidentRetention> logger) : IRetentionSweep
{
    private const string RetentionDaysKey = "Vigil:IncidentRetentionDays";
    private const int DefaultRetentionDays = 365;
    private const int MinRetentionDays = 1;

    public async Task<int> SweepAsync(DateTimeOffset now, CancellationToken ct)
    {
        var retentionDays = Math.Max(MinRetentionDays, configuration.GetValue(RetentionDaysKey, DefaultRetentionDays));
        var cutoff = now.AddDays(-retentionDays);

        var deleted = await db.Incidents.Where(i => i.ResolvedAt != null && i.ResolvedAt < cutoff).ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            logger.LogInformation("Deleted {Count} incidents resolved more than {RetentionDays} days ago", deleted, retentionDays);
        }

        return deleted;
    }
}
