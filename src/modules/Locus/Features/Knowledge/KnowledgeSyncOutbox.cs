using Microsoft.EntityFrameworkCore;
using Sentium.Locus.Data;

namespace Sentium.Locus.Features.Knowledge;

/// <summary>
/// Queues knowledge-base syncs by upserting <see cref="Entities.KnowledgeSyncEntry"/> rows. Call it inside
/// the same transaction as the change that requires the sync, then ring <see cref="KnowledgeSyncSignal"/>
/// after the commit. A single <c>INSERT ... ON CONFLICT</c> keeps concurrent edits of the same asset from
/// racing on the primary key, re-arms failed entries, and bumps the version the processor guards on.
/// </summary>
public sealed class KnowledgeSyncOutbox(LocusDbContext db, TimeProvider timeProvider)
{
    public async Task EnqueueAsync(IReadOnlyCollection<Guid> assetIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(assetIds);

        if (assetIds.Count == 0)
        {
            return;
        }

        var ids = assetIds.Distinct().ToArray();
        var now = timeProvider.GetUtcNow();

        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO locus."KnowledgeSyncEntries" AS e ("AssetId", "Status", "Version", "Attempts", "RequestedAt", "NextAttemptAt", "LastError")
            SELECT id, 'Pending', 1, 0, {now}, {now}, NULL FROM unnest({ids}) AS t(id)
            ON CONFLICT ("AssetId") DO UPDATE SET
                "Status" = 'Pending',
                "Version" = e."Version" + 1,
                "Attempts" = 0,
                "RequestedAt" = EXCLUDED."RequestedAt",
                "NextAttemptAt" = EXCLUDED."NextAttemptAt",
                "LastError" = NULL
            """, ct);
    }
}
