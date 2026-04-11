using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentinel.Application.Engine;
using Sentinel.Core.Events;

namespace Sentinel.Api.Controllers;

[ApiController]
[Authorize]
[Route("events")]
public sealed class EventsController(SentinelPolicyEngine engine) : ControllerBase
{
    [HttpPost]
    public IActionResult Evaluate([FromBody] SentinelEvent evt)
    {
        var decision = engine.Evaluate(evt);

        return decision.Allowed
            ? Ok(decision)
            : Forbid(decision.Reason);
    }
}

