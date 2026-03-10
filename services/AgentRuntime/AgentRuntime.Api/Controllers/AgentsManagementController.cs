using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AgentRuntime.Api.Controllers;

[ApiController]
[Route("agents")]
public class AgentsManagementController(IAgentService agentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAgents(CancellationToken ct)
    {
        var agents = await agentService.GetAgentsAsync(ct);
        return Ok(agents);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAgent([FromBody] CreateAgentRequest request, CancellationToken ct)
    {
        var result = await agentService.CreateAgentAsync(request, ct);
        return CreatedAtAction(nameof(GetAgents), result);
    }
}
