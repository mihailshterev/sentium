using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Sentium.Core.Results;
using Sentium.Identity.Contracts;
using Sentium.Infrastructure.Extensions;
using Sentium.Memoria.Common;
using Sentium.Memoria.Data;
using Sentium.Memoria.Entities;
using Sentium.Memoria.Features.Indexing;
using Sentium.Memoria.Features.Links;

namespace Sentium.Memoria.Features.Sync;

/// <remarks>
/// Each write runs in one transaction with its outbox rows, then rings <see cref="NoteIndexSignal"/> after the commit.
/// The retrying execution strategy may re-run a transaction, so every operation starts from a cleared change tracker.
/// </remarks>
internal sealed partial class VaultSyncService(
    MemoriaDbContext db,
    NoteLinkUpdater linkUpdater,
    NoteIndexOutbox outbox,
    NoteIndexSignal signal,
    IDeviceDirectory deviceDirectory,
    TimeProvider timeProvider) : IVaultSyncService
{
    private const string NotClaimedMessage = "Claim the vault before syncing it.";
    private const string HeldElsewhereMessage = "Another device syncs this vault. Take it over from this device to sync from here.";

    public async Task<DeviceVaultDto?> GetAsync(Guid userId, Guid deviceId, CancellationToken ct = default)
    {
        var vault = await db.Vaults.AsNoTracking().FirstOrDefaultAsync(v => v.UserId == userId, ct);
        return vault is null ? null : await ToDtoAsync(vault, deviceId, ct);
    }

    public async Task<Result<DeviceVaultDto>> ClaimAsync(Guid userId, Guid deviceId, ClaimVaultRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vault = await db.Vaults.FirstOrDefaultAsync(v => v.UserId == userId, ct);

        if (vault is null)
        {
            vault = new Vault
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                CreatedAt = timeProvider.GetUtcNow()
            };
            db.Vaults.Add(vault);
        }
        else if (vault.DeviceId is { } holder && holder != deviceId && !request.Takeover && await DeviceExistsAsync(userId, holder, ct))
        {
            return Result<DeviceVaultDto>.Conflict(HeldElsewhereMessage);
        }

        vault.Name = request.Name.Trim();
        vault.DeviceId = deviceId;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            return Result<DeviceVaultDto>.Conflict("The vault was claimed at the same time from elsewhere; try again.");
        }

        return Result<DeviceVaultDto>.Success(await ToDtoAsync(vault, deviceId, ct));
    }

    public async Task<Result<VaultManifestResponse>> ReconcileAsync(Guid userId, Guid deviceId, VaultManifestRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var manifest = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in request.Notes)
        {
            manifest[VaultPaths.Normalize(entry.Path)!] = entry.Hash.ToLowerInvariant();
        }

        var result = await db.Database.ExecuteInTransactionAsync(() => ReconcileCoreAsync(userId, deviceId, manifest, ct), ct: ct);
        signal.Notify();
        return result;
    }

    public async Task<Result<VaultSyncResult>> RemoveAsync(Guid userId, Guid deviceId, RemoveNotesRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var paths = request.Paths.Select(p => VaultPaths.Normalize(p)!).Distinct(StringComparer.Ordinal).ToArray();

        var result = await db.Database.ExecuteInTransactionAsync(() => RemoveCoreAsync(userId, deviceId, paths, ct), ct: ct);
        signal.Notify();
        return result;
    }

    public async Task<Result<VaultSyncResult>> MoveAsync(Guid userId, Guid deviceId, MoveNoteRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var from = VaultPaths.Normalize(request.From)!;
        var to = VaultPaths.Normalize(request.To)!;

        var result = await db.Database.ExecuteInTransactionAsync(() => MoveCoreAsync(userId, deviceId, from, to, ct), ct: ct);
        signal.Notify();
        return result;
    }

    private async Task<Result<VaultManifestResponse>> ReconcileCoreAsync(Guid userId, Guid deviceId, Dictionary<string, string> manifest, CancellationToken ct)
    {
        db.ChangeTracker.Clear();

        var access = await RequireVaultAsync(userId, deviceId, ct);
        if (access.Value is not { } vault)
        {
            return Propagate<Vault, VaultManifestResponse>(access);
        }

        var existing = await db.Notes
            .AsNoTracking()
            .Where(n => n.VaultId == vault.Id)
            .Select(n => new { n.Id, n.Path, n.ContentHash })
            .ToListAsync(ct);

        var existingHashes = existing.ToDictionary(n => n.Path, n => n.ContentHash, StringComparer.Ordinal);
        var upload = manifest
            .Where(m => !existingHashes.TryGetValue(m.Key, out var hash) || hash != m.Value)
            .Select(m => m.Key)
            .Order(StringComparer.Ordinal)
            .ToList();

        var gone = existing.Where(n => !manifest.ContainsKey(n.Path)).Select(n => n.Id).ToList();
        var removed = await RemoveNotesAsync(vault, gone, ct);

        vault.LastSyncAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);

        return Result<VaultManifestResponse>.Success(new VaultManifestResponse(upload, removed, await CountNotesAsync(vault.Id, ct)));
    }

    private async Task<Result<VaultSyncResult>> RemoveCoreAsync(Guid userId, Guid deviceId, string[] paths, CancellationToken ct)
    {
        db.ChangeTracker.Clear();

        var access = await RequireVaultAsync(userId, deviceId, ct);
        if (access.Value is not { } vault)
        {
            return Propagate<Vault, VaultSyncResult>(access);
        }

        var ids = await db.Notes
            .Where(n => n.VaultId == vault.Id && paths.Contains(n.Path))
            .Select(n => n.Id)
            .ToListAsync(ct);

        var removed = await RemoveNotesAsync(vault, ids, ct);

        vault.LastSyncAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);

        return Result<VaultSyncResult>.Success(new VaultSyncResult(0, 0, removed, await CountNotesAsync(vault.Id, ct)));
    }

    private async Task<Result<VaultSyncResult>> MoveCoreAsync(Guid userId, Guid deviceId, string from, string to, CancellationToken ct)
    {
        db.ChangeTracker.Clear();

        var access = await RequireVaultAsync(userId, deviceId, ct);
        if (access.Value is not { } vault)
        {
            return Propagate<Vault, VaultSyncResult>(access);
        }

        var note = await db.Notes.FirstOrDefaultAsync(n => n.VaultId == vault.Id && n.Path == from, ct);
        if (note is null)
        {
            return Result<VaultSyncResult>.NotFound($"No note at '{from}'.");
        }

        if (from == to)
        {
            return Result<VaultSyncResult>.Success(new VaultSyncResult(0, 1, 0, await CountNotesAsync(vault.Id, ct)));
        }

        var replaced = await db.Notes
            .Where(n => n.VaultId == vault.Id && n.Path == to)
            .Select(n => n.Id)
            .ToListAsync(ct);
        var removed = await RemoveNotesAsync(vault, replaced, ct);

        var oldKey = note.PathKey;
        note.Path = to;
        note.PathKey = VaultPaths.ToKey(to);
        note.Title = VaultPaths.TitleOf(to);
        note.UpdatedAt = timeProvider.GetUtcNow();
        vault.LastSyncAt = note.UpdatedAt;
        await db.SaveChangesAsync(ct);

        var affectedKeys = LinkResolver.KeysAffectedBy(oldKey, note.AliasKeys)
            .Concat(LinkResolver.KeysAffectedBy(note.PathKey, note.AliasKeys))
            .ToList();
        await linkUpdater.ReresolveAsync(vault.Id, userId, affectedKeys, [note.Id], ct);
        await outbox.EnqueueAsync(userId, [note.Id], ct);

        return Result<VaultSyncResult>.Success(new VaultSyncResult(1, 0, removed, await CountNotesAsync(vault.Id, ct)));
    }

    /// <summary>
    /// Deletes notes (their own links cascade; links pointing at them are nulled by the database), re-resolves links
    /// that may now find another note, and queues their knowledge-base removal.
    /// </summary>
    private async Task<int> RemoveNotesAsync(Vault vault, IReadOnlyCollection<Guid> noteIds, CancellationToken ct)
    {
        if (noteIds.Count == 0)
        {
            return 0;
        }

        var ids = noteIds.ToArray();
        var keys = await db.Notes
            .AsNoTracking()
            .Where(n => ids.Contains(n.Id))
            .Select(n => new { n.PathKey, n.AliasKeys })
            .ToListAsync(ct);

        var removed = await db.Notes.Where(n => ids.Contains(n.Id)).ExecuteDeleteAsync(ct);

        var affectedKeys = keys.SelectMany(k => LinkResolver.KeysAffectedBy(k.PathKey, k.AliasKeys)).ToList();
        await linkUpdater.ReresolveAsync(vault.Id, vault.UserId, affectedKeys, [], ct);
        await outbox.EnqueueAsync(vault.UserId, ids, ct);

        return removed;
    }

    private async Task<Result<Vault>> RequireVaultAsync(Guid userId, Guid deviceId, CancellationToken ct)
    {
        var vault = await db.Vaults.FirstOrDefaultAsync(v => v.UserId == userId, ct);

        if (vault is null)
        {
            return Result<Vault>.NotFound(NotClaimedMessage);
        }

        return vault.DeviceId == deviceId ? Result<Vault>.Success(vault) : Result<Vault>.Conflict(HeldElsewhereMessage);
    }

    private async Task<bool> DeviceExistsAsync(Guid userId, Guid deviceId, CancellationToken ct)
    {
        var devices = await deviceDirectory.ListAsync(userId, ct);
        return devices.Any(d => d.Id == deviceId);
    }

    private async Task<DeviceVaultDto> ToDtoAsync(Vault vault, Guid deviceId, CancellationToken ct)
    {
        return new DeviceVaultDto(vault.Id, vault.Name, vault.DeviceId == deviceId, await CountNotesAsync(vault.Id, ct), vault.LastSyncAt);
    }

    private Task<int> CountNotesAsync(Guid vaultId, CancellationToken ct)
    {
        return db.Notes.CountAsync(n => n.VaultId == vaultId, ct);
    }

    private static Result<TOut> Propagate<TIn, TOut>(Result<TIn> result)
    {
        return result.Status switch
        {
            ResultStatus.NotFound => Result<TOut>.NotFound(result.Error),
            ResultStatus.Conflict => Result<TOut>.Conflict(result.Error ?? "Conflict."),
            _ => Result<TOut>.Invalid(result.Error ?? "Invalid request.")
        };
    }

    private static string Sha256Hex(string content)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }
}
