using Microsoft.EntityFrameworkCore;
using Sentium.Core.Text;
using Sentium.Memoria.Contracts.Models;
using Sentium.Memoria.Entities;

namespace Sentium.Memoria.Features.Notes;

internal sealed partial class VaultReader
{
    private const int SnippetMax = 700;
    private const int SimilarityProbeChars = 1_500;

    /// <summary>
    /// How many chunks to fetch per wanted note when filters are applied after the vector search.
    /// </summary>
    private const int FilteredOverfetch = 5;

    public async Task<IReadOnlyList<VaultSearchHit>> SearchAsync(Guid userId, VaultSearchQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (await GetVaultAsync(userId, ct) is not { } vault || string.IsNullOrWhiteSpace(query.Query))
        {
            return [];
        }

        var tag = TagNormalizer.NormalizeOne(query.Tag?.TrimStart('#'));
        var folder = query.Folder?.Trim().Replace('\\', '/').Trim('/').ToLowerInvariant();
        var filtered = tag is not null || !string.IsNullOrEmpty(folder);

        var hits = await semanticSearch.SearchAsync(userId, query.Query.Trim(), query.TopK * (filtered ? FilteredOverfetch : 2), ct);
        var ids = hits.Select(h => h.NoteId).ToArray();

        var notes = db.Notes.AsNoTracking().Where(n => n.UserId == userId && ids.Contains(n.Id));
        if (tag is not null)
        {
            var nested = tag + "/";
            notes = notes.Where(n => n.Tags.Any(t => t == tag || t.StartsWith(nested)));
        }

        if (!string.IsNullOrEmpty(folder))
        {
            var prefix = folder + "/";
            notes = notes.Where(n => n.PathKey.StartsWith(prefix));
        }

        var matches = await notes.ToDictionaryAsync(n => n.Id, ct);

        return [.. hits
            .Where(h => matches.ContainsKey(h.NoteId))
            .Take(query.TopK)
            .Select(h => new VaultSearchHit(ToRef(vault, matches[h.NoteId]), h.Score, Snippet(h.Chunk)))];
    }

    public async Task<IReadOnlyList<VaultRelatedNote>?> GetRelatedAsync(Guid userId, string reference, int topK, CancellationToken ct = default)
    {
        if (await GetVaultAsync(userId, ct) is not { } vault || await FindNoteAsync(userId, reference, ct) is not { } note)
        {
            return null;
        }

        var (outgoing, incoming) = await LoadNeighboursAsync(userId, note.Id, ct);
        var neighbours = outgoing.Concat(incoming).ToHashSet();
        var shared = await CountSharedNeighboursAsync(userId, note.Id, neighbours, ct);
        var similar = await FindSimilarAsync(userId, note, topK, ct);

        var related = new List<(Guid Id, VaultRelation Relation, double Strength)>();
        related.AddRange(outgoing.Take(topK).Select(id => (id, VaultRelation.LinksTo, 1d)));
        related.AddRange(incoming.Where(id => !outgoing.Contains(id)).Take(topK).Select(id => (id, VaultRelation.LinkedFrom, 1d)));
        related.AddRange(shared.Take(topK).Select(s => (s.Key, VaultRelation.SharesLinks, (double)s.Value)));
        related.AddRange(similar.Select(s => (s.NoteId, VaultRelation.Similar, (double)s.Score)));

        return await ToRelatedAsync(vault, related, ct);
    }

    public async Task<IReadOnlyList<VaultRelatedNote>?> SuggestLinksAsync(Guid userId, string reference, int topK, CancellationToken ct = default)
    {
        if (await GetVaultAsync(userId, ct) is not { } vault || await FindNoteAsync(userId, reference, ct) is not { } note)
        {
            return null;
        }

        var (outgoing, incoming) = await LoadNeighboursAsync(userId, note.Id, ct);
        var similar = await FindSimilarAsync(userId, note, topK + outgoing.Count + incoming.Count, ct);

        var unlinked = similar
            .Where(s => !outgoing.Contains(s.NoteId) && !incoming.Contains(s.NoteId))
            .Take(topK)
            .Select(s => (s.NoteId, VaultRelation.Similar, (double)s.Score));

        return await ToRelatedAsync(vault, unlinked, ct);
    }

    private async Task<(HashSet<Guid> Outgoing, HashSet<Guid> Incoming)> LoadNeighboursAsync(Guid userId, Guid noteId, CancellationToken ct)
    {
        var outgoing = await db.Links
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.SourceNoteId == noteId && l.TargetNoteId != null && l.TargetNoteId != noteId)
            .Select(l => l.TargetNoteId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var incoming = await db.Links
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.TargetNoteId == noteId && l.SourceNoteId != noteId)
            .Select(l => l.SourceNoteId)
            .Distinct()
            .ToListAsync(ct);

        return ([.. outgoing], [.. incoming]);
    }

    /// <summary>
    /// Notes two hops away (through any neighbour, in either link direction), ranked by how many neighbours they
    /// share with the note.
    /// </summary>
    private async Task<List<KeyValuePair<Guid, int>>> CountSharedNeighboursAsync(Guid userId, Guid noteId, HashSet<Guid> neighbours, CancellationToken ct)
    {
        if (neighbours.Count == 0)
        {
            return [];
        }

        var ids = neighbours.ToArray();
        var pairs = await db.Links
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.TargetNoteId != null && (ids.Contains(l.SourceNoteId) || ids.Contains(l.TargetNoteId!.Value)))
            .Select(l => new { l.SourceNoteId, TargetNoteId = l.TargetNoteId!.Value })
            .ToListAsync(ct);

        var counts = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var pair in pairs)
        {
            if (neighbours.Contains(pair.SourceNoteId))
            {
                Count(counts, pair.TargetNoteId, pair.SourceNoteId);
            }

            if (neighbours.Contains(pair.TargetNoteId))
            {
                Count(counts, pair.SourceNoteId, pair.TargetNoteId);
            }
        }

        return [.. counts
            .Where(c => c.Key != noteId && !neighbours.Contains(c.Key))
            .Select(c => new KeyValuePair<Guid, int>(c.Key, c.Value.Count))
            .OrderByDescending(c => c.Value)];

        static void Count(Dictionary<Guid, HashSet<Guid>> counts, Guid candidate, Guid via)
        {
            if (!counts.TryGetValue(candidate, out var set))
            {
                set = [];
                counts[candidate] = set;
            }

            set.Add(via);
        }
    }

    private async Task<IEnumerable<SemanticNoteHit>> FindSimilarAsync(Guid userId, VaultNote note, int topK, CancellationToken ct)
    {
        var probe = $"{note.Title}\n\n{note.Content}";
        if (probe.Length > SimilarityProbeChars)
        {
            probe = probe[..SimilarityProbeChars];
        }

        var hits = await semanticSearch.SearchAsync(userId, probe, (topK + 1) * 3, ct);
        return hits.Where(h => h.NoteId != note.Id).Take(topK);
    }

    private async Task<IReadOnlyList<VaultRelatedNote>> ToRelatedAsync(Vault vault, IEnumerable<(Guid Id, VaultRelation Relation, double Strength)> related, CancellationToken ct)
    {
        var list = related.ToList();
        var refs = await LoadRefsAsync(vault, list.Select(r => r.Id), ct);

        return [.. list
            .Where(r => refs.ContainsKey(r.Id))
            .Select(r => new VaultRelatedNote(refs[r.Id], r.Relation, Math.Round(r.Strength, 3)))];
    }

    private static string Snippet(string chunk)
    {
        return chunk.Length > SnippetMax ? chunk[..SnippetMax] + "…" : chunk;
    }
}
