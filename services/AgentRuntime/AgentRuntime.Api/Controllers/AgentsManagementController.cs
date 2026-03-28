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

    [HttpGet("{agentId:guid}")]
    public async Task<IActionResult> GetAgentById(Guid agentId, CancellationToken ct)
    {
        var agent = await agentService.GetAgentByIdAsync(agentId, ct);
        return Ok(agent);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAgent([FromBody] CreateAgentRequest request, CancellationToken ct)
    {
        var result = await agentService.CreateAgentAsync(request, ct);
        return CreatedAtAction(nameof(GetAgents), result);
    }

    [HttpPut("{agentId:guid}")]
    public async Task<IActionResult> UpdateAgent(Guid agentId, [FromBody] UpdateAgentRequest request, CancellationToken ct)
    {
        await agentService.UpdateAgentAsync(agentId, request, ct);
        return NoContent();
    }

    [HttpDelete("{agentId:guid}")]
    public async Task<IActionResult> DeleteAgent(Guid agentId, CancellationToken ct)
    {
        await agentService.DeleteAgentAsync(agentId, ct);
        return NoContent();
    }
}
