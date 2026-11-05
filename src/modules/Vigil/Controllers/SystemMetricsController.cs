using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Vigil.Features.Metrics;

namespace Sentium.Vigil.Controllers;

[ApiController]
[Authorize]
[Route($"{VigilRoutes.Prefix}/system")]
public sealed class SystemMetricsController(IVigil vigil) : ControllerBase
{
    /// <summary>
    /// Returns a current snapshot of host system metrics.
    /// </summary>
    [HttpGet("metrics")]
    public IActionResult GetMetrics()
    {
        return Ok(vigil.GetMetrics());
    }
}
