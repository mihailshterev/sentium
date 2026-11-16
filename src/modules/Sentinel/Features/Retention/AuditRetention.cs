using Sentium.Infrastructure.Retention;
using Sentium.Sentinel.Features.Audit;

namespace Sentium.Sentinel.Features.Retention;

/// <summary>
/// Deletes PDP audit records older than <c>Sentinel:AuditRetentionDays</c> (default 180). Every agent tool call writes
/// one, so without this the table only grows.
/// </summary>
internal sealed class AuditRetention(IAuditLog auditLog, IConfiguration configuration, ILogger<AuditRetention> logger) : IRetentionSweep
{
    private const string RetentionDaysKey = "Sentinel:AuditRetentionDays";
    private const int DefaultRetentionDays = 180;
    private const int MinRetentionDays = 1;

    public async Task<int> SweepAsync(DateTimeOffset now, CancellationToken ct)
    {
        var retentionDays = Math.Max(MinRetentionDays, configuration.GetValue(RetentionDaysKey, DefaultRetentionDays));

        var deleted = await auditLog.PurgeOlderThanAsync(now.AddDays(-retentionDays), ct);

        if (deleted > 0)
        {
            logger.LogInformation("Deleted {Count} audit records older than {RetentionDays} days", deleted, retentionDays);
        }

        return deleted;
    }
}
