using Microsoft.EntityFrameworkCore;
using Sentium.Infrastructure.Retention;
using Sentium.Nuntius.Common;
using Sentium.Nuntius.Data;

namespace Sentium.Nuntius.Features.Retention;

/// <summary>
/// Deletes Notifications last published more than the retention period ago (<c>Nuntius:RetentionDays</c>, default
/// 90), read or not.
/// </summary>
internal sealed class NotificationRetention(NuntiusDbContext db, IConfiguration configuration, ILogger<NotificationRetention> logger) : IRetentionSweep
{
    private const string RetentionDaysKey = "Nuntius:RetentionDays";
    private const int MinRetentionDays = 1;

    public async Task<int> SweepAsync(DateTimeOffset now, CancellationToken ct)
    {
        var retentionDays = configuration.GetValue(RetentionDaysKey, NuntiusLimits.DefaultRetentionDays);
        var cutoff = now.AddDays(-Math.Max(MinRetentionDays, retentionDays));

        var deleted = await db.Notifications
            .Where(n => n.UpdatedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            logger.LogInformation("Deleted {Count} notifications older than {RetentionDays} days", deleted, retentionDays);
        }

        return deleted;
    }
}
