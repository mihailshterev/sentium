using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sentium.Core.Results;
using Sentium.Core.Text;
using Sentium.Identity.Contracts;
using Sentium.Memoria.Common;
using Sentium.Memoria.Contracts.Models;
using Sentium.Memoria.Data;
using Sentium.Memoria.Entities;

namespace Sentium.Memoria.Features.Notes;

internal sealed class VaultQueryService(
    MemoriaDbContext db,
    VaultReader reader,
    VaultEraser eraser,
    IDeviceDirectory deviceDirectory) : IVaultQueryService
{
    public const int DefaultGraphNodes = 1_500;
    public const int MaxGraphNodes = 5_000;

    /// <summary>
    /// A vault note shown in the portal is never cut; the limit only guards the response size.
    /// </summary>
    private const int NoteDisplayMax = MemoriaLimits.NoteContentMax;

    public async Task<VaultSummaryDto?> GetSummaryAsync(Guid userId, CancellationToken ct = default)
    {
        if (await reader.GetVaultAsync(userId, ct) is not { } vault || await reader.GetOverviewAsync(userId, ct) is not { } overview)
        {
            return null;
        }

        var deviceName = vault.DeviceId is { } deviceId
            ? (await deviceDirectory.ListAsync(userId, ct)).FirstOrDefault(d => d.Id == deviceId)?.Name
            : null;

        var failed = await db.IndexEntries.CountAsync(e => e.UserId == userId && e.Status == NoteIndexStatus.Failed, ct);

        return new VaultSummaryDto(
            vault.Id,
            vault.Name,
            deviceName,
            overview.NoteCount,
            overview.LinkCount,
            overview.UnresolvedLinkCount,
            overview.OrphanCount,
            overview.PendingIndexCount - failed,
            failed,
            vault.LastSyncAt);
    }

    public async Task<PagedResponse<VaultNoteSummaryDto>> ListNotesAsync(Guid userId, VaultNotesQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (page, pageSize) = query.Normalize();
        var notes = db.Notes.AsNoTracking().Where(n => n.UserId == userId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = SqlLike.Contains(query.Search.Trim());
            notes = notes.Where(n => EF.Functions.ILike(n.Path, pattern, SqlLike.EscapeCharacter));
        }

        if (TagNormalizer.NormalizeOne(query.Tag?.TrimStart('#')) is { } tag)
        {
            var nested = tag + "/";
            notes = notes.Where(n => n.Tags.Any(t => t == tag || t.StartsWith(nested)));
        }

        if (query.Folder?.Trim().Replace('\\', '/').Trim('/').ToLowerInvariant() is { Length: > 0 } folder)
        {
            var prefix = folder + "/";
            notes = notes.Where(n => n.PathKey.StartsWith(prefix));
        }

        var total = await notes.CountAsync(ct);
        var items = await notes
            .OrderBy(n => n.PathKey)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new VaultNoteSummaryDto(n.Id, n.Path, n.Title, n.Tags, n.WordCount, n.UpdatedAt, n.IndexedHash != null && !db.IndexEntries.Any(e => e.NoteId == n.Id)))
            .ToListAsync(ct);

        return PagedResponse<VaultNoteSummaryDto>.Create(items, total, page, pageSize);
    }

    public async Task<VaultNoteDto?> GetNoteAsync(Guid userId, Guid noteId, CancellationToken ct = default)
    {
        var vault = await reader.GetVaultAsync(userId, ct);
        var note = vault is null ? null : await db.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.UserId == userId && n.Id == noteId, ct);
        if (vault is null || note is null)
        {
            return null;
        }

        var detail = await reader.BuildDetailAsync(vault, note, NoteDisplayMax, ct);

        return new VaultNoteDto(
            note.Id,
            note.Path,
            note.Title,
            detail.Note.ObsidianUri,
            detail.Content,
            note.Tags,
            note.Aliases,
            ParseFrontmatter(note.FrontmatterJson),
            note.WordCount,
            note.UpdatedAt,
            note.IndexedAt,
            detail.Links,
            detail.Backlinks);
    }

    public async Task<IReadOnlyList<VaultTagCount>> GetTagsAsync(Guid userId, CancellationToken ct = default)
    {
        var tagLists = await db.Notes.AsNoTracking().Where(n => n.UserId == userId).Select(n => n.Tags).ToListAsync(ct);

        return [.. tagLists
            .SelectMany(t => t)
            .GroupBy(t => t, StringComparer.Ordinal)
            .Select(g => new VaultTagCount(g.Key, g.Count()))
            .OrderByDescending(t => t.Count)
            .ThenBy(t => t.Tag, StringComparer.Ordinal)];
    }

    public async Task<VaultGraphDto> GetGraphAsync(Guid userId, string? tag, int limit, CancellationToken ct = default)
    {
        var nodeLimit = Math.Clamp(limit, 1, MaxGraphNodes);

        var notes = db.Notes.AsNoTracking().Where(n => n.UserId == userId);
        if (TagNormalizer.NormalizeOne(tag?.TrimStart('#')) is { } normalizedTag)
        {
            var nested = normalizedTag + "/";
            notes = notes.Where(n => n.Tags.Any(t => t == normalizedTag || t.StartsWith(nested)));
        }

        var nodes = await notes.Select(n => new { n.Id, n.Title, n.Path, n.Tags }).ToListAsync(ct);
        var nodeIds = nodes.Select(n => n.Id).ToHashSet();

        var edges = (await db.Links
                .AsNoTracking()
                .Where(l => l.UserId == userId && l.TargetNoteId != null && l.TargetNoteId != l.SourceNoteId)
                .Select(l => new { l.SourceNoteId, TargetNoteId = l.TargetNoteId!.Value })
                .Distinct()
                .ToListAsync(ct))
            .Where(e => nodeIds.Contains(e.SourceNoteId) && nodeIds.Contains(e.TargetNoteId))
            .ToList();

        var degree = new Dictionary<Guid, int>();
        foreach (var edge in edges)
        {
            degree[edge.SourceNoteId] = degree.GetValueOrDefault(edge.SourceNoteId) + 1;
            degree[edge.TargetNoteId] = degree.GetValueOrDefault(edge.TargetNoteId) + 1;
        }

        var shown = nodes
            .OrderByDescending(n => degree.GetValueOrDefault(n.Id))
            .ThenBy(n => n.Path, StringComparer.Ordinal)
            .Take(nodeLimit)
            .ToList();
        var shownIds = shown.Select(n => n.Id).ToHashSet();

        return new VaultGraphDto(
            [.. shown.Select(n => new VaultGraphNodeDto(n.Id, n.Title, n.Path, VaultPaths.FolderOf(n.Path), n.Tags.FirstOrDefault(), degree.GetValueOrDefault(n.Id)))],
            [.. edges.Where(e => shownIds.Contains(e.SourceNoteId) && shownIds.Contains(e.TargetNoteId)).Select(e => new VaultGraphEdgeDto(e.SourceNoteId, e.TargetNoteId))],
            nodes.Count > shown.Count);
    }

    public Task<bool> ForgetAsync(Guid userId, CancellationToken ct = default) => eraser.EraseAsync(userId, ct);

    private static JsonElement? ParseFrontmatter(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
