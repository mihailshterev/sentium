using Microsoft.EntityFrameworkCore;
using Sentium.Memoria.Common;
using Sentium.Memoria.Contracts.Models;
using Sentium.Memoria.Data;
using Sentium.Memoria.Entities;
using Sentium.Memoria.Features.Parsing;

namespace Sentium.Memoria.Features.Notes;

/// <summary>
/// Reads one user's vault for the portal and for assistants. Every query filters on the explicit user id, so it is
/// equally safe in a request scope and in the system scope the agent facade uses.
/// </summary>
internal sealed partial class VaultReader(MemoriaDbContext db, VaultSemanticSearch semanticSearch)
{
    private const int TopTagCount = 15;
    private const int SampleSize = 10;

    public async Task<Vault?> GetVaultAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.Vaults.AsNoTracking().FirstOrDefaultAsync(v => v.UserId == userId, ct);
    }

    /// <summary>
    /// Finds a note the way a wikilink to <paramref name="reference"/> would, then by alias, then by a title
    /// containing it.
    /// </summary>
    public async Task<VaultNote?> FindNoteAsync(Guid userId, string reference, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        var target = reference.Trim().TrimStart('!').Trim('[', ']');
        target = target.Split('|')[0].Split('#')[0];

        if (MarkdownNoteParser.ToTargetKey(target) is not { } key)
        {
            return null;
        }

        var suffix = "/" + key;
        var byPath = await db.Notes
            .AsNoTracking()
            .Where(n => n.UserId == userId && (n.PathKey == key || n.PathKey.EndsWith(suffix)))
            .OrderBy(n => n.PathKey == key ? 0 : 1)
            .ThenBy(n => n.PathKey.Length)
            .ThenBy(n => n.PathKey)
            .FirstOrDefaultAsync(ct);

        if (byPath is not null)
        {
            return byPath;
        }

        var byAlias = await db.Notes
            .AsNoTracking()
            .Where(n => n.UserId == userId && n.AliasKeys.Contains(key))
            .OrderBy(n => n.PathKey.Length)
            .FirstOrDefaultAsync(ct);

        if (byAlias is not null)
        {
            return byAlias;
        }

        var pattern = SqlLike.Contains(target.Trim());
        return await db.Notes
            .AsNoTracking()
            .Where(n => n.UserId == userId && EF.Functions.ILike(n.Title, pattern, SqlLike.EscapeCharacter))
            .OrderBy(n => n.Title.Length)
            .ThenBy(n => n.PathKey)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<VaultNoteDetail?> GetNoteDetailAsync(Guid userId, string reference, int maxChars, CancellationToken ct = default)
    {
        var vault = await GetVaultAsync(userId, ct);
        var note = vault is null ? null : await FindNoteAsync(userId, reference, ct);

        return note is null ? null : await BuildDetailAsync(vault!, note, maxChars, ct);
    }

    public async Task<VaultNoteDetail> BuildDetailAsync(Vault vault, VaultNote note, int maxChars, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vault);
        ArgumentNullException.ThrowIfNull(note);

        var links = await db.Links
            .AsNoTracking()
            .Where(l => l.UserId == vault.UserId && l.SourceNoteId == note.Id)
            .OrderBy(l => l.Target)
            .Select(l => new { l.Target, l.Heading, l.IsEmbed, l.TargetNoteId })
            .ToListAsync(ct);

        var backlinkIds = await db.Links
            .AsNoTracking()
            .Where(l => l.UserId == vault.UserId && l.TargetNoteId == note.Id && l.SourceNoteId != note.Id)
            .Select(l => l.SourceNoteId)
            .Distinct()
            .ToListAsync(ct);

        var refs = await LoadRefsAsync(vault, links.Where(l => l.TargetNoteId != null).Select(l => l.TargetNoteId!.Value).Concat(backlinkIds), ct);

        var truncated = note.Content.Length > maxChars;
        return new VaultNoteDetail(
            ToRef(vault, note),
            truncated ? note.Content[..maxChars] : note.Content,
            truncated,
            note.Aliases,
            [.. links.Select(l => new VaultLinkInfo(l.Target, l.Heading, l.IsEmbed, l.TargetNoteId is { } id ? refs.GetValueOrDefault(id) : null))],
            [.. backlinkIds.Select(id => refs.GetValueOrDefault(id)).OfType<VaultNoteRef>().OrderBy(r => r.Path, StringComparer.Ordinal)],
            note.UpdatedAt);
    }

    public async Task<VaultOverview?> GetOverviewAsync(Guid userId, CancellationToken ct = default)
    {
        if (await GetVaultAsync(userId, ct) is not { } vault)
        {
            return null;
        }

        var tagLists = await db.Notes.AsNoTracking().Where(n => n.UserId == userId).Select(n => n.Tags).ToListAsync(ct);
        var topTags = tagLists
            .SelectMany(t => t)
            .GroupBy(t => t, StringComparer.Ordinal)
            .Select(g => new VaultTagCount(g.Key, g.Count()))
            .OrderByDescending(t => t.Count)
            .ThenBy(t => t.Tag, StringComparer.Ordinal)
            .Take(TopTagCount)
            .ToList();

        var orphans = OrphansQuery(userId);
        var orphanCount = await orphans.CountAsync(ct);
        var orphanSample = await orphans.OrderBy(n => n.Path).Take(SampleSize).ToListAsync(ct);

        var unresolved = db.Links.AsNoTracking().Where(l => l.UserId == userId && l.TargetNoteId == null);
        var unresolvedTargets = await unresolved
            .GroupBy(l => l.TargetKey)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Max(l => l.Target)!)
            .Take(SampleSize)
            .ToListAsync(ct);

        return new VaultOverview(
            vault.Name,
            tagLists.Count,
            await db.Links.CountAsync(l => l.UserId == userId, ct),
            vault.LastSyncAt,
            topTags,
            orphanCount,
            [.. orphanSample.Select(n => ToRef(vault, n))],
            await unresolved.CountAsync(ct),
            unresolvedTargets,
            await db.IndexEntries.CountAsync(e => e.UserId == userId, ct));
    }

    public async Task<Dictionary<Guid, VaultNoteRef>> LoadRefsAsync(Vault vault, IEnumerable<Guid> noteIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vault);

        var ids = noteIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        var notes = await db.Notes
            .AsNoTracking()
            .Where(n => n.UserId == vault.UserId && ids.Contains(n.Id))
            .Select(n => new { n.Id, n.Path, n.Title, n.Tags })
            .ToListAsync(ct);

        return notes.ToDictionary(n => n.Id, n => new VaultNoteRef(n.Id, n.Path, n.Title, ObsidianLinks.Open(vault.Name, n.Path), n.Tags));
    }

    public static VaultNoteRef ToRef(Vault vault, VaultNote note)
    {
        ArgumentNullException.ThrowIfNull(vault);
        ArgumentNullException.ThrowIfNull(note);

        return new VaultNoteRef(note.Id, note.Path, note.Title, ObsidianLinks.Open(vault.Name, note.Path), note.Tags);
    }

    /// <summary>
    /// Notes with no resolved link in either direction.
    /// </summary>
    private IQueryable<VaultNote> OrphansQuery(Guid userId)
    {
        return db.Notes
            .AsNoTracking()
            .Where(n => n.UserId == userId
                && !db.Links.Any(l => l.SourceNoteId == n.Id && l.TargetNoteId != null)
                && !db.Links.Any(l => l.TargetNoteId == n.Id));
    }
}
