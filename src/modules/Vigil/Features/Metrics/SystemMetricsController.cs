using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sentium.Vigil.Features.Metrics;

[ApiController]
[Authorize]
[Route($"{VigilRoutes.Prefix}/system")]
internal sealed class SystemMetricsController(IVigil vigil) : ControllerBase
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
