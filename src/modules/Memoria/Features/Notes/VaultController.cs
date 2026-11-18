using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;
using Sentium.Infrastructure.Security;
using Sentium.Memoria.Contracts.Models;

namespace Sentium.Memoria.Features.Notes;

/// <summary>
/// The signed-in user's synced Obsidian vault. Private to its owner: a Sovereign sees only their own vault here.
/// </summary>
[ApiController]
[Authorize]
[Route($"{MemoriaRoutes.Prefix}/vault")]
internal sealed class VaultController(IVaultQueryService queryService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(VaultSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        return await queryService.GetSummaryAsync(userId, ct) is { } summary ? Ok(summary) : NoContent();
    }

    [HttpGet("notes")]
    [ProducesResponseType(typeof(PagedResponse<VaultNoteSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListNotes([FromQuery] VaultNotesQuery query, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await queryService.ListNotesAsync(userId, query, ct));
    }

    [HttpGet("notes/{id:guid}")]
    [ProducesResponseType(typeof(VaultNoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNote(Guid id, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        return await queryService.GetNoteAsync(userId, id, ct) is { } note ? Ok(note) : NotFound();
    }

    [HttpGet("tags")]
    [ProducesResponseType(typeof(IReadOnlyList<VaultTagCount>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTags(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await queryService.GetTagsAsync(userId, ct));
    }

    /// <summary>
    /// The note graph: notes as nodes, resolved links as edges. Past <paramref name="limit"/> notes the best-linked
    /// ones are kept.
    /// </summary>
    [HttpGet("graph")]
    [ProducesResponseType(typeof(VaultGraphDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGraph([FromQuery] string? tag, [FromQuery] int limit = VaultQueryService.DefaultGraphNodes, CancellationToken ct = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await queryService.GetGraphAsync(userId, tag, limit, ct));
    }

    /// <summary>
    /// Forgets the synced vault. Nothing changes in Obsidian; Satelles re-claims and re-uploads it on its next sync
    /// unless the vault is removed there too.
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Forget(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        return await queryService.ForgetAsync(userId, ct) ? NoContent() : NotFound();
    }
}
