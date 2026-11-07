using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;
using Sentium.Infrastructure.Results;
using Sentium.Locus.Dtos;
using Sentium.Locus.Features.Assets;

namespace Sentium.Locus.Controllers;

/// <summary>
/// The household's assets: search, catalogue, edit, and control what assistants may see.
/// </summary>
[ApiController]
[Authorize]
[Route($"{LocusRoutes.Prefix}/assets")]
public sealed class AssetsController(IAssetService assetService) : ControllerBase
{
    /// <summary>
    /// Searches assets. Filters combine with AND; repeat <c>tag</c> to require several tags.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AssetSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchAssets([FromQuery] AssetSearchQuery query, CancellationToken ct)
        => Ok(await assetService.SearchAssetsAsync(query, ct));

    /// <summary>
    /// Retrieves an asset with its location path, attachments and knowledge-base status.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AssetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsset(Guid id, CancellationToken ct)
    {
        var asset = await assetService.GetAssetAsync(id, ct);
        return asset is null ? NotFound() : Ok(asset);
    }

    /// <summary>
    /// Catalogues a new asset.
    /// </summary>
    /// <response code="400">Invalid data or the location does not exist.</response>
    [HttpPost]
    [ProducesResponseType(typeof(AssetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAsset([FromBody] SaveAssetRequest request, CancellationToken ct)
    {
        var result = await assetService.CreateAssetAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetAsset), new { id = result.Value!.Id }, result.Value)
            : result.ToActionResult(this);
    }

    /// <summary>
    /// Updates an asset (including moving it to another location).
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AssetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsset(Guid id, [FromBody] SaveAssetRequest request, CancellationToken ct)
        => (await assetService.UpdateAssetAsync(id, request, ct)).ToActionResult(this);

    /// <summary>
    /// Deletes an asset, its attachments and its knowledge-base copy.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsset(Guid id, CancellationToken ct)
        => await assetService.DeleteAssetAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>
    /// Queues the asset's knowledge-base sync again (use after a failed sync).
    /// </summary>
    [HttpPost("{id:guid}/knowledge-sync")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestKnowledgeSync(Guid id, CancellationToken ct)
        => await assetService.RequestKnowledgeSyncAsync(id, ct) ? Accepted() : NotFound();
}
