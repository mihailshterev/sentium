using Sentium.Locus.Core.Assets;
using Sentium.Locus.Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sentium.Locus.Api.Controllers;

/// <summary>
/// API controller for managing assets in the Environmental Knowledge Base (Digital Twin inventory).
/// Supports all items from smart appliances to documents and safety equipment.
/// </summary>
[ApiController]
[Authorize]
[Route("assets")]
public sealed class AssetsController(IAssetService assetService) : ControllerBase
{
    /// <summary>Retrieves all catalogued assets.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssets(CancellationToken ct)
    {
        var assets = await assetService.GetAssetsAsync(ct);
        return Ok(assets);
    }

    /// <summary>Retrieves all assets in a specific location.</summary>
    [HttpGet("by-location/{locationId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssetsByLocation(Guid locationId, CancellationToken ct)
    {
        var assets = await assetService.GetAssetsByLocationAsync(locationId, ct);
        return Ok(assets);
    }

    /// <summary>Retrieves a specific asset by its identifier.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
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

    /// <summary>Creates a new asset in the inventory.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
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

    /// <summary>Updates an existing asset.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
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

    /// <summary>Deletes an asset from the inventory.</summary>
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
