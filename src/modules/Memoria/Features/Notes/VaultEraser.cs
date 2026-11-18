using Microsoft.EntityFrameworkCore;
using Sentium.Infrastructure.Extensions;
using Sentium.Memoria.Data;
using Sentium.Memoria.Features.Indexing;

namespace Sentium.Memoria.Features.Notes;

/// <summary>
/// Deletes a user's vault with all its notes and links, and queues the removal of their knowledge-base chunks in the
/// same transaction so no chunk outlives its note.
/// </summary>
internal sealed class VaultEraser(MemoriaDbContext db, NoteIndexOutbox outbox, NoteIndexSignal signal)
{
    public async Task<bool> EraseAsync(Guid userId, CancellationToken ct = default)
    {
        var erased = await db.Database.ExecuteInTransactionAsync(() => EraseCoreAsync(userId, ct), ct: ct);
        if (erased)
        {
            signal.Notify();
        }

        return erased;
    }

    private async Task<bool> EraseCoreAsync(Guid userId, CancellationToken ct)
    {
        var noteIds = await db.Notes.IgnoreQueryFilters().Where(n => n.UserId == userId).Select(n => n.Id).ToListAsync(ct);
        await outbox.EnqueueAsync(userId, noteIds, ct);

        return await db.Vaults.IgnoreQueryFilters().Where(v => v.UserId == userId).ExecuteDeleteAsync(ct) > 0;
    }
}
