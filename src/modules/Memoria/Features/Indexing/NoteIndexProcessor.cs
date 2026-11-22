using Microsoft.EntityFrameworkCore;
using Sentium.Infrastructure.BackgroundWork;
using Sentium.Codex.Contracts;
using Sentium.Codex.Contracts.Models;
using Sentium.Memoria.Common;
using Sentium.Memoria.Data;
using Sentium.Memoria.Entities;

namespace Sentium.Memoria.Features.Indexing;

/// <summary>
/// Brings one note's knowledge-base copy in line with the database. Resolve it from a fresh, system-activated scope
/// per note.
/// </summary>
internal sealed class NoteIndexProcessor(
    MemoriaDbContext db,
    IDocumentIngestionService ingestion,
    NoteIndexOutbox outbox,
    TimeProvider timeProvider,
    ILogger<NoteIndexProcessor> logger) : IOutboxProcessor
{
    public const int BatchSize = 20;

    static int IOutboxProcessor.BatchSize => BatchSize;

    Task IOutboxProcessor.ProcessEntryAsync(Guid id, CancellationToken ct) => ProcessAsync(id, ct);

    public async Task<IReadOnlyList<Guid>> GetDueAsync(int limit, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();

        return await db.IndexEntries
            .AsNoTracking()
            .Where(e => e.Status == NoteIndexStatus.Pending && e.NextAttemptAt <= now)
            .OrderBy(e => e.NextAttemptAt)
            .Select(e => e.NoteId)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<DateTimeOffset?> GetNextDueAtAsync(CancellationToken ct = default)
    {
        return await db.IndexEntries
            .AsNoTracking()
            .Where(e => e.Status == NoteIndexStatus.Pending)
            .OrderBy(e => e.NextAttemptAt)
            .Select(e => (DateTimeOffset?)e.NextAttemptAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<NoteIndexOutcome> ProcessAsync(Guid noteId, CancellationToken ct = default)
    {
        var entry = await db.IndexEntries.AsNoTracking().FirstOrDefaultAsync(e => e.NoteId == noteId, ct);
        if (entry is null)
        {
            return NoteIndexOutcome.Skipped;
        }

        try
        {
            var outcome = await IndexAsync(noteId, ct);

            // Only clear the entry if no sync re-queued the note while we were working on it.
            await db.IndexEntries
                .Where(e => e.NoteId == noteId && e.Version == entry.Version)
                .ExecuteDeleteAsync(ct);

            return outcome;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            await RecordFailureAsync(entry, ex, ct);
            return NoteIndexOutcome.Failed;
        }
    }

    /// <summary>
    /// Queues every note whose knowledge-base copy is missing or stale (a crash between a sync and its indexing, or a
    /// new <see cref="NoteKnowledgeDocument"/> format). Run once at startup.
    /// </summary>
    public async Task<int> ReconcileAsync(CancellationToken ct = default)
    {
        var notes = await db.Notes
            .AsNoTracking()
            .Select(n => new { n.Id, n.UserId, n.Path, n.ContentHash, n.IndexedHash })
            .ToListAsync(ct);

        var staleByUser = notes
            .Where(n => NoteKnowledgeDocument.ComputeHash(n.Path, n.ContentHash) != n.IndexedHash)
            .GroupBy(n => n.UserId);

        var count = 0;
        foreach (var group in staleByUser)
        {
            var ids = group.Select(n => n.Id).ToList();
            await outbox.EnqueueAsync(group.Key, ids, ct);
            count += ids.Count;
        }

        return count;
    }

    private async Task<NoteIndexOutcome> IndexAsync(Guid noteId, CancellationToken ct)
    {
        var note = await db.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == noteId, ct);

        if (note is null)
        {
            // Needs no embedding model, so removals work even while the embedding server is down.
            await ingestion.RemoveDocumentAsync(NoteKnowledgeDocument.NoteIdKey, noteId.ToString(), KnowledgeCollections.VaultNotes, ct);
            return NoteIndexOutcome.Removed;
        }

        var hash = NoteKnowledgeDocument.ComputeHash(note.Path, note.ContentHash);
        if (string.Equals(hash, note.IndexedHash, StringComparison.Ordinal))
        {
            return NoteIndexOutcome.Unchanged;
        }

        var request = NoteKnowledgeDocument.Build(note);
        await ingestion.ReplaceAsync(request, NoteKnowledgeDocument.NoteIdKey, KnowledgeCollections.VaultNotes, ct);

        // A sync that changed the note meanwhile re-queued it; leave its hash stale so that run re-indexes.
        var indexedAt = timeProvider.GetUtcNow();
        await db.Notes
            .Where(n => n.Id == noteId && n.ContentHash == note.ContentHash && n.Path == note.Path)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IndexedHash, hash)
                .SetProperty(n => n.IndexedAt, indexedAt), ct);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Indexed vault note {NoteId} ({ChunkCount} chunks)", noteId, request.Chunks?.Count ?? 0);
        }

        return NoteIndexOutcome.Indexed;
    }

    private async Task RecordFailureAsync(NoteIndexEntry entry, Exception exception, CancellationToken ct)
    {
        var attempts = entry.Attempts + 1;
        var status = attempts >= RetryBackoff.MaxAttempts ? NoteIndexStatus.Failed : NoteIndexStatus.Pending;
        var nextAttemptAt = timeProvider.GetUtcNow() + RetryBackoff.DelayAfter(attempts);
        var error = exception.Message.Length > MemoriaLimits.IndexErrorMax ? exception.Message[..MemoriaLimits.IndexErrorMax] : exception.Message;

        logger.LogWarning(exception, "Indexing vault note {NoteId} failed (attempt {Attempt}/{MaxAttempts})", entry.NoteId, attempts, RetryBackoff.MaxAttempts);

        await db.IndexEntries
            .Where(e => e.NoteId == entry.NoteId && e.Version == entry.Version)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Attempts, attempts)
                .SetProperty(e => e.Status, status)
                .SetProperty(e => e.NextAttemptAt, nextAttemptAt)
                .SetProperty(e => e.LastError, error), ct);
    }
}
