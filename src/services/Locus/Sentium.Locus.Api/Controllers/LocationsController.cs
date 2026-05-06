using Sentium.Locus.Core.Dtos;
using Sentium.Locus.Core.Locations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sentium.Locus.Api.Controllers;

/// <summary>
/// API controller for managing home locations in the Environmental Knowledge Base.
/// Locations form a hierarchical structure representing the physical spaces of the home.
/// </summary>
[ApiController]
[Authorize]
[Route("locations")]
public sealed class LocationsController(ILocationService locationService) : ControllerBase
{
    /// <summary>Retrieves all top-level and nested locations.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLocations(CancellationToken ct)
    {
        var locations = await locationService.GetLocationsAsync(ct);
        return Ok(locations);
    }

    /// <summary>Retrieves a specific location by its identifier.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLocation(Guid id, CancellationToken ct)
    {
        var location = await locationService.GetLocationAsync(id, ct);
        if (location is null)
        {
            return NotFound();
        }

        return Ok(location);
    }

    /// <summary>Retrieves all direct child locations of a given parent location.</summary>
    [HttpGet("{id:guid}/sub-locations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubLocations(Guid id, CancellationToken ct)
    {
        var subLocations = await locationService.GetSubLocationsAsync(id, ct);
        return Ok(subLocations);
    }

    /// <summary>Creates a new location.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateLocation([FromBody] CreateLocationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Location name is required." });
        }

        var location = await locationService.CreateLocationAsync(request, ct);
        if (location is null)
        {
            return Conflict(new { error = $"A location named '{request.Name}' already exists under the same parent." });
        }

        return CreatedAtAction(nameof(GetLocation), new { id = location.Id }, location);
    }

    /// <summary>Updates an existing location.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateLocation(Guid id, [FromBody] UpdateLocationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Location name is required." });
        }

        try
        {
            var location = await locationService.UpdateLocationAsync(id, request, ct);
            if (location is null)
            {
                return NotFound();
            }

            return Ok(location);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>Deletes a location.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLocation(Guid id, CancellationToken ct)
    {
        var deleted = await locationService.DeleteLocationAsync(id, ct);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}
