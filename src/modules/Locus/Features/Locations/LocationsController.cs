using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Infrastructure.Results;
using Sentium.Locus.Features.Locations;

namespace Sentium.Locus.Features.Locations;

/// <summary>
/// The household's location tree (Home → Garage → Shelf B). Shared by every signed-in household member.
/// </summary>
[ApiController]
[Authorize]
[Route($"{LocusRoutes.Prefix}/locations")]
public sealed class LocationsController(ILocationService locationService) : ControllerBase
{
    /// <summary>
    /// Lists every location as a flat list ordered by path; build the tree from <c>parentLocationId</c>.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<LocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLocations(CancellationToken ct)
        => Ok(await locationService.GetLocationsAsync(ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LocationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLocation(Guid id, CancellationToken ct)
    {
        var location = await locationService.GetLocationAsync(id, ct);
        return location is null ? NotFound() : Ok(location);
    }

    /// <summary>
    /// Creates a location, optionally under a parent.
    /// </summary>
    /// <response code="400">The parent does not exist or the request is invalid.</response>
    /// <response code="409">A sibling already has this name, or the tree would become too deep.</response>
    [HttpPost]
    [ProducesResponseType(typeof(LocationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateLocation([FromBody] SaveLocationRequest request, CancellationToken ct)
    {
        var result = await locationService.CreateLocationAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetLocation), new { id = result.Value!.Id }, result.Value)
            : result.ToActionResult(this);
    }

    /// <summary>
    /// Renames, edits or moves a location (with everything below it).
    /// </summary>
    /// <response code="409">Duplicate sibling name, a move into its own subtree, or the tree would become too deep.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(LocationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateLocation(Guid id, [FromBody] SaveLocationRequest request, CancellationToken ct)
        => (await locationService.UpdateLocationAsync(id, request, ct)).ToActionResult(this);

    /// <summary>
    /// Deletes a location. A location that still holds sub-locations or assets is only deleted when
    /// <paramref name="moveContentsTo"/> names where its direct contents should go.
    /// </summary>
    /// <response code="409">Not empty (the response carries <c>childCount</c> and <c>assetCount</c>), or the move is not allowed.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteLocation(Guid id, [FromQuery] Guid? moveContentsTo, CancellationToken ct)
    {
        var result = await locationService.DeleteLocationAsync(id, moveContentsTo, ct);

        return result.Status switch
        {
            LocationDeleteStatus.Deleted => NoContent(),
            LocationDeleteStatus.NotFound => NotFound(),
            LocationDeleteStatus.InvalidTarget => Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest, title: "Invalid request"),
            _ => Conflict(new ProblemDetails
            {
                Title = result.Status == LocationDeleteStatus.NotEmpty ? "Location is not empty" : "Conflict",
                Detail = result.Error,
                Status = StatusCodes.Status409Conflict,
                Extensions =
                {
                    ["childCount"] = result.ChildCount,
                    ["assetCount"] = result.AssetCount
                }
            })
        };
    }
}
