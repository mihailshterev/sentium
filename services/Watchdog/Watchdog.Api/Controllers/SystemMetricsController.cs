using Microsoft.AspNetCore.Mvc;
using Watchdog.Core.Metrics;

namespace Watchdog.Api.Controllers;

[ApiController]
[Route("system")]
public sealed class SystemMetricsController(IWatchdog watchdog) : ControllerBase
{
    [HttpGet("metrics")]
    public IActionResult GetMetrics()
    {
        return Ok(watchdog.GetMetrics());
    }
}
