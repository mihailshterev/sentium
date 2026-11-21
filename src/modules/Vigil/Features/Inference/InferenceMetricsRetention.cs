using Microsoft.EntityFrameworkCore;
using Sentium.Infrastructure.Retention;
using Sentium.Vigil.Data;

namespace Sentium.Vigil.Features.Inference;

/// <summary>
/// Deletes inference metrics older than <c>Vigil:InferenceMetrics:RetentionDays</c> (default 30).
/// </summary>
internal sealed class InferenceMetricsRetention(VigilDbContext db, IConfiguration configuration, ILogger<InferenceMetricsRetention> logger) : IRetentionSweep
{
    private const int DefaultRetentionDays = 30;
    private const string RetentionDaysKey = "Vigil:InferenceMetrics:RetentionDays";
    private const int MinRetentionDays = 1;

    public async Task<int> SweepAsync(DateTimeOffset now, CancellationToken ct)
    {
        var retentionDays = Math.Max(MinRetentionDays, configuration.GetValue(RetentionDaysKey, DefaultRetentionDays));
        var cutoff = now.AddDays(-retentionDays);
        var cutoffMinute = EpochMinutes.From(cutoff);

        var deleted = await db.InferenceServerMinutes.Where(r => r.EpochMinute < cutoffMinute).ExecuteDeleteAsync(ct);
        deleted += await db.InferenceUsageMinutes.Where(r => r.EpochMinute < cutoffMinute).ExecuteDeleteAsync(ct);
        deleted += await db.ModelLoadEvents.Where(e => e.CompletedAt < cutoff).ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            logger.LogInformation("Deleted {Count} inference metric rows older than {RetentionDays} days", deleted, retentionDays);
        }

        return deleted;
    }
}
