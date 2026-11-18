using Microsoft.AspNetCore.Mvc;
using Sentium.Infrastructure.Results;
using Sentium.Infrastructure.Security;
using Sentium.Memoria.Common;

namespace Sentium.Memoria.Features.Sync;

/// <summary>
/// Where Sentium Satelles syncs the user's Obsidian vault. Device token only: the portal reads the vault through
/// <c>VaultController</c>.
/// </summary>
[ApiController]
[AuthorizeDevice]
[Route($"{MemoriaRoutes.Prefix}/device/vault")]
internal sealed class VaultSyncController(IVaultSyncService syncService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(DeviceVaultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (!TryGetCaller(out var userId, out var deviceId))
        {
            return Unauthorized();
        }

        return await syncService.GetAsync(userId, deviceId, ct) is { } vault ? Ok(vault) : NoContent();
    }

    [HttpPut]
    [ProducesResponseType(typeof(DeviceVaultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Claim([FromBody] ClaimVaultRequest request, CancellationToken ct)
    {
        if (!TryGetCaller(out var userId, out var deviceId))
        {
            return Unauthorized();
        }

        return (await syncService.ClaimAsync(userId, deviceId, request, ct)).ToActionResult(this);
    }

    [HttpPost("manifest")]
    [RequestSizeLimit(MemoriaLimits.SyncRequestBytesMax)]
    [ProducesResponseType(typeof(VaultManifestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reconcile([FromBody] VaultManifestRequest request, CancellationToken ct)
    {
        if (!TryGetCaller(out var userId, out var deviceId))
        {
            return Unauthorized();
        }

        return (await syncService.ReconcileAsync(userId, deviceId, request, ct)).ToActionResult(this);
    }

    [HttpPut("notes")]
    [RequestSizeLimit(MemoriaLimits.SyncRequestBytesMax)]
    [ProducesResponseType(typeof(VaultSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Upload([FromBody] UploadNotesRequest request, CancellationToken ct)
    {
        if (!TryGetCaller(out var userId, out var deviceId))
        {
            return Unauthorized();
        }

        return (await syncService.UploadAsync(userId, deviceId, request, ct)).ToActionResult(this);
    }

    [HttpPost("notes/remove")]
    [ProducesResponseType(typeof(VaultSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove([FromBody] RemoveNotesRequest request, CancellationToken ct)
    {
        if (!TryGetCaller(out var userId, out var deviceId))
        {
            return Unauthorized();
        }

        return (await syncService.RemoveAsync(userId, deviceId, request, ct)).ToActionResult(this);
    }

    [HttpPost("notes/move")]
    [ProducesResponseType(typeof(VaultSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Move([FromBody] MoveNoteRequest request, CancellationToken ct)
    {
        if (!TryGetCaller(out var userId, out var deviceId))
        {
            return Unauthorized();
        }

        return (await syncService.MoveAsync(userId, deviceId, request, ct)).ToActionResult(this);
    }

    private bool TryGetCaller(out Guid userId, out Guid deviceId)
    {
        userId = currentUser.UserId ?? Guid.Empty;
        deviceId = User.GetDeviceId() ?? Guid.Empty;
        return userId != Guid.Empty && deviceId != Guid.Empty;
    }
}
