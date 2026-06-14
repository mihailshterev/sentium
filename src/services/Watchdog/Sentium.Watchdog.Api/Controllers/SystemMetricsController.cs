using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Watchdog.Core.Metrics;

namespace Sentium.Watchdog.Api.Controllers;

[ApiController]
[Authorize]
[Route("system")]
public sealed class SystemMetricsController(IWatchdog watchdog) : ControllerBase
{
    /// <summary>
    /// Returns a current snapshot of host system metrics.
    /// </summary>
    [HttpGet("metrics")]
    public IActionResult GetMetrics()
    {
        return Ok(watchdog.GetMetrics());
    }
}
