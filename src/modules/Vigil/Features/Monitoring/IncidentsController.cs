using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sentium.Vigil.Features.Monitoring;

[ApiController]
[Authorize]
[Route($"{VigilRoutes.Prefix}/incidents")]
public sealed class IncidentsController(IIncidentStore incidentStore) : ControllerBase
{
    /// <summary>
    /// Returns open incidents followed by recently resolved ones, newest first.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<Incident>>(StatusCodes.Status200OK)]
    public IActionResult GetAll() => Ok(incidentStore.GetAll());
}
