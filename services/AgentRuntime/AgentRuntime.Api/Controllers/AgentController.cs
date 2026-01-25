using AgentRuntime.Application.Workflow;
using Microsoft.AspNetCore.Mvc;

namespace AgentRuntime.Api.Controllers;

[ApiController]
[Route("agents")]
public class AgentController : ControllerBase
{
    [HttpPost("prompt")]
    public async Task<IActionResult> PromptAgent([FromBody] string input, CancellationToken ct)
    {
        var result = await AgentExecutor.ExecuteWorkflowAsync(input, ct);
        return Ok(result);
    }

    [HttpGet("health")]
    public async Task<IActionResult> AgentHealthCheck(CancellationToken ct)
    {
        return Ok("Agent service is healthy.");
    }
}
