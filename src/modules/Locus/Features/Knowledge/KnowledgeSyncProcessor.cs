using Microsoft.EntityFrameworkCore;
using Sentium.Codex.Contracts;
using Sentium.Codex.Contracts.Models;
using Sentium.Locus.Common;
using Sentium.Locus.Data;
using Sentium.Locus.Entities;
using Sentium.Locus.Features.Locations;

namespace Sentium.Locus.Features.Knowledge;

/// <summary>
/// Brings one asset's knowledge-base copy in line with the database. Resolve it from a fresh,
/// system-activated scope per asset (the knowledge base's own context must not carry state across items).
/// </summary>
internal sealed class KnowledgeSyncProcessor(
    LocusDbContext db,
    IDocumentIngestionService ingestion,
    KnowledgeSyncOutbox outbox,
    TimeProvider timeProvider,
    ILogger<KnowledgeSyncProcessor> logger)
{
    public const int BatchSize = 20;

    public async Task<IReadOnlyList<Guid>> GetDueAsync(int limit, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();

        return await db.KnowledgeSyncEntries
            .AsNoTracking()
            .Where(e => e.Status == KnowledgeSyncStatus.Pending && e.NextAttemptAt <= now)
            .OrderBy(e => e.NextAttemptAt)
            .Select(e => e.AssetId)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<DateTimeOffset?> GetNextDueAtAsync(CancellationToken ct = default)
    {
        return await db.KnowledgeSyncEntries
            .AsNoTracking()
            .Where(e => e.Status == KnowledgeSyncStatus.Pending)
            .OrderBy(e => e.NextAttemptAt)
            .Select(e => (DateTimeOffset?)e.NextAttemptAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<KnowledgeSyncOutcome> ProcessAsync(Guid assetId, CancellationToken ct = default)
    {
        var entry = await db.KnowledgeSyncEntries.AsNoTracking().FirstOrDefaultAsync(e => e.AssetId == assetId, ct);
        if (entry is null)
        {
            return KnowledgeSyncOutcome.Skipped;
        }

        try
        {
            var outcome = await SyncAsync(assetId, ct);

            // Only clear the entry if nobody re-queued the asset while we were working on it.
            await db.KnowledgeSyncEntries
                .Where(e => e.AssetId == assetId && e.Version == entry.Version)
                .ExecuteDeleteAsync(ct);

            return outcome;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            await RecordFailureAsync(entry, ex, ct);
            return KnowledgeSyncOutcome.Failed;
        }
    }

    /// <summary>
    /// Queues every asset whose knowledge-base copy is out of date: shared assets whose content hash no
    /// longer matches (edits missed during downtime, renamed locations, a new document format) and
    /// private assets that still have a copy. Run once at startup.
    /// </summary>
    public async Task<int> ReconcileAsync(CancellationToken ct = default)
    {
        var tree = await db.LoadLocationTreeAsync(ct);
        var assets = await db.Assets.AsNoTracking().ToListAsync(ct);

        var stale = assets
            .Where(a => a.IsAgentAccessible
                ? AssetKnowledgeDocumentBuilder.ComputeHash(AssetKnowledgeDocumentBuilder.Build(a, tree.GetPathString(a.LocationId))) != a.KnowledgeContentHash
                : a.KnowledgeContentHash is not null)
            .Select(a => a.Id)
            .ToList();

        await outbox.EnqueueAsync(stale, ct);
        return stale.Count;
    }

    private async Task<KnowledgeSyncOutcome> SyncAsync(Guid assetId, CancellationToken ct)
    {
        var source = AssetKnowledgeDocumentBuilder.SourceFor(assetId);
        var asset = await db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == assetId, ct);

        if (asset is null || !asset.IsAgentAccessible)
        {
            // Needs no embedding model, so revoking access works even while the embedding server is down.
            await ingestion.RemoveBySourceAsync(source, ct: ct);

            if (asset?.KnowledgeContentHash is not null)
            {
                await db.Assets
                    .Where(a => a.Id == assetId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(a => a.KnowledgeContentHash, (string?)null)
                        .SetProperty(a => a.KnowledgeSyncedAt, (DateTimeOffset?)null), ct);
            }

            return KnowledgeSyncOutcome.Removed;
        }

        var tree = await db.LoadLocationTreeAsync(ct);
        var path = tree.GetPathString(asset.LocationId);
        var sections = AssetKnowledgeDocumentBuilder.Build(asset, path);
        var hash = AssetKnowledgeDocumentBuilder.ComputeHash(sections);

        if (string.Equals(hash, asset.KnowledgeContentHash, StringComparison.Ordinal))
        {
            return KnowledgeSyncOutcome.Unchanged;
        }

        // Re-ingesting a source adds rows, so replace rather than append.
        await ingestion.RemoveBySourceAsync(source, ct: ct);

        var metadata = AssetKnowledgeDocumentBuilder.BuildMetadata(asset, path);
        foreach (var section in sections)
        {
            await ingestion.IngestAsync(new IngestionRequest
            {
                Content = section,
                Source = source,
                SourceType = IngestionSourceType.Inventory,
                Metadata = new Dictionary<string, string>(metadata),
                Scope = KnowledgeScope.Shared,
                UserId = null
            }, ct: ct);
        }

        var syncedAt = timeProvider.GetUtcNow();
        await db.Assets
            .Where(a => a.Id == assetId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.KnowledgeContentHash, hash)
                .SetProperty(a => a.KnowledgeSyncedAt, syncedAt), ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Synced asset {AssetId} to the knowledge base ({SectionCount} sections)", assetId, sections.Count);
        }

        return KnowledgeSyncOutcome.Synced;
    }

    private async Task RecordFailureAsync(KnowledgeSyncEntry entry, Exception exception, CancellationToken ct)
    {
        var attempts = entry.Attempts + 1;
        var status = attempts >= KnowledgeSyncBackoff.MaxAttempts ? KnowledgeSyncStatus.Failed : KnowledgeSyncStatus.Pending;
        var nextAttemptAt = timeProvider.GetUtcNow() + KnowledgeSyncBackoff.DelayAfter(attempts);
        var error = exception.Message.Length > LocusLimits.SyncErrorMax ? exception.Message[..LocusLimits.SyncErrorMax] : exception.Message;

        logger.LogWarning(exception, "Knowledge sync for asset {AssetId} failed (attempt {Attempt}/{MaxAttempts})", entry.AssetId, attempts, KnowledgeSyncBackoff.MaxAttempts);

        await db.KnowledgeSyncEntries
            .Where(e => e.AssetId == entry.AssetId && e.Version == entry.Version)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Attempts, attempts)
                .SetProperty(e => e.Status, status)
                .SetProperty(e => e.NextAttemptAt, nextAttemptAt)
                .SetProperty(e => e.LastError, error), ct);
    }
}
