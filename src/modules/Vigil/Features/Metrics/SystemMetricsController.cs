using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sentium.Vigil.Features.Metrics;

[ApiController]
[Authorize]
[Route($"{VigilRoutes.Prefix}/system")]
internal sealed class SystemMetricsController(IVigil vigil) : ControllerBase
{
    [HttpGet("metrics")]
    public IActionResult GetMetrics()
    {
        return Ok(vigil.GetMetrics());
    }
}
