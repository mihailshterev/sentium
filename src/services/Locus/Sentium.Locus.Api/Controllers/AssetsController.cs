using Sentium.Locus.Core.Assets;
using Sentium.Locus.Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sentium.Locus.Api.Controllers;

/// <summary>
/// Manages the Environmental Knowledge Base (EKB) inventory.
/// </summary>
/// <remarks>
/// Tracks the Digital Twin state of physical and virtual assets, ranging from
/// smart hardware and sensors to static documents and items.
/// </remarks>
[ApiController]
[Authorize]
[Route("assets")]
public sealed class AssetsController(IAssetService assetService) : ControllerBase
{
    /// <summary>
    /// Retrieves a list of all catalogued assets in the system.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AssetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssets(CancellationToken ct)
    {
        var assets = await assetService.GetAssetsAsync(ct);
        return Ok(assets);
    }

    /// <summary>
    /// Retrieves all assets associated with a specific location.
    /// </summary>
    /// <param name="locationId">The unique identifier of the room or zone.</param>
    [HttpGet("by-location/{locationId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AssetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssetsByLocation(Guid locationId, CancellationToken ct)
    {
        var assets = await assetService.GetAssetsByLocationAsync(locationId, ct);
        return Ok(assets);
    }

    /// <summary>
    /// Retrieves the details of a specific asset.
    /// </summary>
    /// <param name="id">The unique identifier of the asset.</param>
    /// <response code="200">Returns the requested asset.</response>
    /// <response code="404">If the asset does not exist.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AssetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsset(Guid id, CancellationToken ct)
    {
        var asset = await assetService.GetAssetAsync(id, ct);
        if (asset is null)
        {
            return NotFound();
        }

        return Ok(asset);
    }

    /// <summary>
    /// Catalogues a new asset into the inventory.
    /// </summary>
    /// <param name="request">The asset configuration and initial location.</param>
    /// <response code="201">The asset was successfully created.</response>
    /// <response code="400">If the request data is invalid.</response>
    /// <response code="404">If the specified location does not exist.</response>
    [HttpPost]
    [ProducesResponseType(typeof(AssetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateAsset([FromBody] CreateAssetRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest(new { error = "Asset display name is required." });
        }

        var asset = await assetService.CreateAssetAsync(request, ct);
        if (asset is null)
        {
            return NotFound(new { error = $"Location '{request.LocationId}' does not exist." });
        }

        return CreatedAtAction(nameof(GetAsset), new { id = asset.Id }, asset);
    }

    /// <summary>
    /// Updates the properties or location of an existing asset.
    /// </summary>
    /// <response code="200">Returns the updated asset state.</response>
    /// <response code="400">If the update data is invalid.</response>
    /// <response code="404">If the asset or the new target location is not found.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AssetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsset(Guid id, [FromBody] UpdateAssetRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest(new { error = "Asset display name is required." });
        }

        try
        {
            var asset = await assetService.UpdateAssetAsync(id, request, ct);
            if (asset is null)
            {
                return NotFound();
            }

            return Ok(asset);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Removes an asset from the inventory.
    /// </summary>
    /// <response code="204">Asset successfully removed.</response>
    /// <response code="404">If the asset does not exist.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsset(Guid id, CancellationToken ct)
    {
        var deleted = await assetService.DeleteAssetAsync(id, ct);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}
