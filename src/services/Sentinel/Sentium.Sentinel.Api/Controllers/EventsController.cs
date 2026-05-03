using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Sentinel.Application.Engine;
using Sentium.Sentinel.Core.Events;
using Sentium.Sentinel.Core.Stores;

namespace Sentium.Sentinel.Api.Controllers;

[ApiController]
[Authorize]
[Route("events")]
public sealed class EventsController(SentinelPolicyEngine engine, INetworkEventStore eventStore) : ControllerBase
{
    [HttpPost]
    public IActionResult Evaluate([FromBody] SentinelEvent evt)
    {
        var decision = engine.Evaluate(evt);
        return decision.Allowed ? Ok(decision) : Forbid(decision.Reason);
    }

    [HttpGet("network")]
    public IActionResult GetNetworkEvents([FromQuery] int count = 100)
    {
        var events = eventStore.GetRecent(Math.Min(count, 200));
        return Ok(events);
    }
}

