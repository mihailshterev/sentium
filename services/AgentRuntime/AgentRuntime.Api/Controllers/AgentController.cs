using Microsoft.AspNetCore.Mvc;

namespace AgentRuntime.Api.Controllers;

[ApiController]
[Route("agents")]
public class AgentController : ControllerBase
{
    [HttpGet("health")]
    public async Task<IActionResult> AgentHealthCheck(CancellationToken ct)
    {
        return Ok("Agent service is healthy.");
    }
}
