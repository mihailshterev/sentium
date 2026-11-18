using Microsoft.EntityFrameworkCore;
using Sentium.Memoria.Data;

namespace Sentium.Memoria.Features.Links;

/// <summary>
/// Keeps <see cref="Entities.NoteLink.TargetNoteId"/> current after notes are added, moved or removed, touching only
/// the links whose resolution can have changed.
/// </summary>
internal sealed class NoteLinkUpdater(MemoriaDbContext db)
{
    /// <param name="affectedKeys">Link keys that can now resolve differently (see <see cref="LinkResolver.KeysAffectedBy"/>).</param>
    /// <param name="affectedNoteIds">Notes whose own links are new, or that links may currently point at.</param>
    public async Task<int> ReresolveAsync(
        Guid vaultId,
        Guid userId,
        IReadOnlyCollection<string> affectedKeys,
        IReadOnlyCollection<Guid> affectedNoteIds,
        CancellationToken ct)
    {
        if (affectedKeys.Count == 0 && affectedNoteIds.Count == 0)
        {
            return 0;
        }

        var keys = affectedKeys.Distinct(StringComparer.Ordinal).ToArray();
        var noteIds = affectedNoteIds.Distinct().ToArray();

        var links = await db.Links
            .Where(l => l.UserId == userId
                && (keys.Contains(l.TargetKey)
                    || noteIds.Contains(l.SourceNoteId)
                    || (l.TargetNoteId != null && noteIds.Contains(l.TargetNoteId.Value))))
            .ToListAsync(ct);

        if (links.Count == 0)
        {
            return 0;
        }

        var resolver = await LoadResolverAsync(vaultId, ct);
        var changed = 0;

        foreach (var link in links)
        {
            var target = resolver.Resolve(link.TargetKey);
            if (target != link.TargetNoteId)
            {
                link.TargetNoteId = target;
                changed++;
            }
        }

        await db.SaveChangesAsync(ct);
        return changed;
    }

    private async Task<LinkResolver> LoadResolverAsync(Guid vaultId, CancellationToken ct)
    {
        var notes = await db.Notes
            .AsNoTracking()
            .Where(n => n.VaultId == vaultId)
            .Select(n => new { n.Id, n.PathKey, n.AliasKeys })
            .ToListAsync(ct);

        return new LinkResolver(notes.Select(n => new NoteKey(n.Id, n.PathKey, n.AliasKeys)));
    }
}
