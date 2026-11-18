using Sentium.Core.Results;

namespace Sentium.Memoria.Features.Sync;

/// <summary>
/// The Device side of a vault: Sentium Satelles claims the user's vault, reconciles it against a manifest and then
/// streams note changes. Every call except the claim is refused (Conflict) unless the calling Device holds the vault.
/// </summary>
internal interface IVaultSyncService
{
    /// <summary>
    /// The user's vault as this Device sees it, or <see langword="null"/> before any Device claimed one.
    /// </summary>
    Task<DeviceVaultDto?> GetAsync(Guid userId, Guid deviceId, CancellationToken ct = default);

    /// <summary>
    /// Creates the user's vault or moves it to this Device. Conflict when another paired Device holds it and
    /// <see cref="ClaimVaultRequest.Takeover"/> is not set.
    /// </summary>
    Task<Result<DeviceVaultDto>> ClaimAsync(Guid userId, Guid deviceId, ClaimVaultRequest request, CancellationToken ct = default);

    Task<Result<VaultManifestResponse>> ReconcileAsync(Guid userId, Guid deviceId, VaultManifestRequest request, CancellationToken ct = default);

    /// <summary>
    /// Adds or replaces notes. Invalid when a note's hash does not match its content.
    /// </summary>
    Task<Result<VaultSyncResult>> UploadAsync(Guid userId, Guid deviceId, UploadNotesRequest request, CancellationToken ct = default);

    /// <summary>
    /// Removes notes; unknown paths are ignored.
    /// </summary>
    Task<Result<VaultSyncResult>> RemoveAsync(Guid userId, Guid deviceId, RemoveNotesRequest request, CancellationToken ct = default);

    /// <summary>
    /// Renames a note, keeping its id so links and backlinks follow it. A note already at the destination is replaced;
    /// NotFound when nothing is at the source.
    /// </summary>
    Task<Result<VaultSyncResult>> MoveAsync(Guid userId, Guid deviceId, MoveNoteRequest request, CancellationToken ct = default);
}
