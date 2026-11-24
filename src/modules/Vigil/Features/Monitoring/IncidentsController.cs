using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sentium.Vigil.Features.Monitoring;

[ApiController]
[Authorize]
[Route($"{VigilRoutes.Prefix}/incidents")]
internal sealed class IncidentsController(IIncidentStore incidentStore) : ControllerBase
{
    private const int DefaultLimit = 200;
    private const int MaxLimit = 1000;

    /// <summary>
    /// Returns open incidents followed by the most recently resolved ones, newest first.
    /// </summary>
    /// <param name="limit">How many resolved incidents to include (default 200, max 1000).</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<Incident>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int limit = DefaultLimit, CancellationToken ct = default)
    {
        return Ok(await incidentStore.GetRecentAsync(Math.Clamp(limit, 1, MaxLimit), ct));
    }
}
