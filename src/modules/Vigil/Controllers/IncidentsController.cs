using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Vigil.Features.Monitoring;

namespace Sentium.Vigil.Controllers;

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
