using Microsoft.AspNetCore.Mvc;
using Sentinel.Application.Engine;
using Sentinel.Core.Events;

namespace Sentinel.Api.Controllers;

[ApiController]
[Route("events")]
public sealed class EventsController : ControllerBase
{
    private readonly SentinelPolicyEngine Engine;

    public EventsController(SentinelPolicyEngine engine)
    {
        Engine = engine;
    }

    [HttpPost]
    public IActionResult Evaluate([FromBody] SentinelEvent evt)
    {
        var decision = Engine.Evaluate(evt);

        return decision.Allowed
            ? Ok(decision)
            : Forbid(decision.Reason);
    }
}

