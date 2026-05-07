using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Watchdog.Core.Monitoring;

namespace Sentium.Watchdog.Api.Controllers;

[ApiController]
[Authorize]
[Route("status")]
public sealed class WatchdogStatusController(IServiceHealthStateStore stateStore) : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll() => Ok(stateStore.GetAll());

    [HttpGet("{serviceName}")]
    public IActionResult Get(string serviceName)
    {
        var status = stateStore.Get(serviceName);
        return status is null ? NotFound() : Ok(status);
    }
}
