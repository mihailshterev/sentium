using Microsoft.EntityFrameworkCore;
using Sentium.Core.Results;
using Sentium.Infrastructure.Extensions;
using Sentium.Memoria.Common;
using Sentium.Memoria.Entities;
using Sentium.Memoria.Features.Links;
using Sentium.Memoria.Features.Parsing;

namespace Sentium.Memoria.Features.Sync;

internal sealed partial class VaultSyncService
{
    public async Task<Result<VaultSyncResult>> UploadAsync(Guid userId, Guid deviceId, UploadNotesRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Last upload of a path wins, as on disk.
        var uploads = request.Notes
            .Select(n => n with { Path = VaultPaths.Normalize(n.Path)!, Hash = n.Hash.ToLowerInvariant() })
            .GroupBy(n => n.Path, StringComparer.Ordinal)
            .Select(g => g.Last())
            .ToList();

        if (uploads.FirstOrDefault(n => Sha256Hex(n.Content) != n.Hash) is { } mismatch)
        {
            return Result<VaultSyncResult>.Invalid($"The hash of '{mismatch.Path}' does not match its content.");
        }

        var result = await db.Database.ExecuteInTransactionAsync(() => UploadCoreAsync(userId, deviceId, uploads, ct), ct: ct);
        signal.Notify();
        return result;
    }

    private async Task<Result<VaultSyncResult>> UploadCoreAsync(Guid userId, Guid deviceId, List<NoteUpload> uploads, CancellationToken ct)
    {
        db.ChangeTracker.Clear();

        var access = await RequireVaultAsync(userId, deviceId, ct);
        if (access.Value is not { } vault)
        {
            return Propagate<Vault, VaultSyncResult>(access);
        }

        var paths = uploads.Select(u => u.Path).ToArray();
        var existing = await db.Notes
            .Where(n => n.VaultId == vault.Id && paths.Contains(n.Path))
            .ToDictionaryAsync(n => n.Path, StringComparer.Ordinal, ct);

        var noteCount = await CountNotesAsync(vault.Id, ct);
        var added = uploads.Count(u => !existing.ContainsKey(u.Path));
        if (noteCount + added > MemoriaLimits.NotesPerVaultMax)
        {
            return Result<VaultSyncResult>.Conflict($"A vault may hold at most {MemoriaLimits.NotesPerVaultMax} notes.");
        }

        var now = timeProvider.GetUtcNow();
        var changed = new List<(VaultNote Note, ParsedNote Parsed)>();
        var affectedKeys = new List<string>();

        foreach (var upload in uploads)
        {
            existing.TryGetValue(upload.Path, out var note);
            if (note is not null && note.ContentHash == upload.Hash)
            {
                continue;
            }

            if (note is null)
            {
                note = new VaultNote { Id = Guid.CreateVersion7(), VaultId = vault.Id, UserId = userId, Path = upload.Path };
                db.Notes.Add(note);
            }
            else
            {
                // Aliases the note drops must stop resolving to it.
                affectedKeys.AddRange(note.AliasKeys);
            }

            var parsed = MarkdownNoteParser.Parse(upload.Path, upload.Content);
            Apply(note, upload, parsed, now);
            affectedKeys.AddRange(LinkResolver.KeysAffectedBy(note.PathKey, note.AliasKeys));
            changed.Add((note, parsed));
        }

        if (changed.Count > 0)
        {
            await SaveChangedNotesAsync(vault, changed, affectedKeys, ct);
        }

        vault.LastSyncAt = now;
        await db.SaveChangesAsync(ct);

        return Result<VaultSyncResult>.Success(new VaultSyncResult(
            changed.Count,
            uploads.Count - changed.Count,
            0,
            noteCount + added));
    }

    private async Task SaveChangedNotesAsync(Vault vault, List<(VaultNote Note, ParsedNote Parsed)> changed, List<string> affectedKeys, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);

        var changedIds = changed.Select(c => c.Note.Id).ToArray();
        await db.Links.Where(l => changedIds.Contains(l.SourceNoteId)).ExecuteDeleteAsync(ct);

        foreach (var (note, parsed) in changed)
        {
            db.Links.AddRange(parsed.Links.Select(link => new NoteLink
            {
                Id = Guid.CreateVersion7(),
                SourceNoteId = note.Id,
                UserId = vault.UserId,
                Target = link.Target.Length > MemoriaLimits.PathMax ? link.Target[..MemoriaLimits.PathMax] : link.Target,
                TargetKey = link.TargetKey,
                Heading = link.Heading,
                IsEmbed = link.IsEmbed
            }));
        }

        await db.SaveChangesAsync(ct);

        await linkUpdater.ReresolveAsync(vault.Id, vault.UserId, affectedKeys, changedIds, ct);
        await outbox.EnqueueAsync(vault.UserId, changedIds, ct);
    }

    private static void Apply(VaultNote note, NoteUpload upload, ParsedNote parsed, DateTimeOffset now)
    {
        note.PathKey = VaultPaths.ToKey(upload.Path);
        note.Title = Truncate(VaultPaths.TitleOf(upload.Path), MemoriaLimits.TitleMax);
        note.ContentHash = upload.Hash;
        note.Content = upload.Content;
        note.Tags = [.. parsed.Tags];
        note.Aliases = [.. parsed.Aliases];
        note.AliasKeys = [.. parsed.Aliases.Select(a => a.ToLowerInvariant()).Distinct(StringComparer.Ordinal)];
        note.FrontmatterJson = parsed.FrontmatterJson;
        note.WordCount = parsed.WordCount;
        note.UpdatedAt = now;
    }

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;
}
