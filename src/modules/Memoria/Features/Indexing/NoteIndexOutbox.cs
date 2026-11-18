using Microsoft.EntityFrameworkCore;
using Sentium.Memoria.Data;

namespace Sentium.Memoria.Features.Indexing;

/// <summary>
/// Queues knowledge-base updates by upserting <see cref="Entities.NoteIndexEntry"/> rows. Call it inside the same
/// transaction as the sync that requires the update, then ring <see cref="NoteIndexSignal"/> after the commit. A
/// single <c>INSERT ... ON CONFLICT</c> re-arms failed entries and bumps the version the processor guards on.
/// </summary>
internal sealed class NoteIndexOutbox(MemoriaDbContext db, TimeProvider timeProvider)
{
    public async Task EnqueueAsync(Guid userId, IReadOnlyCollection<Guid> noteIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(noteIds);

        if (noteIds.Count == 0)
        {
            return;
        }

        var ids = noteIds.Distinct().ToArray();
        var now = timeProvider.GetUtcNow();

        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO memoria."IndexEntries" AS e ("NoteId", "UserId", "Status", "Version", "Attempts", "RequestedAt", "NextAttemptAt", "LastError")
            SELECT id, {userId}, 'Pending', 1, 0, {now}, {now}, NULL FROM unnest({ids}) AS t(id)
            ON CONFLICT ("NoteId") DO UPDATE SET
                "Status" = 'Pending',
                "Version" = e."Version" + 1,
                "Attempts" = 0,
                "RequestedAt" = EXCLUDED."RequestedAt",
                "NextAttemptAt" = EXCLUDED."NextAttemptAt",
                "LastError" = NULL
            """, ct);
    }
}
