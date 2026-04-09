using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AgentRuntime.Api.Controllers;

[ApiController]
[Route("agents")]
public sealed class AgentsManagementController(IAgentService agentService) : ControllerBase
{
    [HttpGet]
    public async ValueTask<IActionResult> GetAgents(CancellationToken ct)
    {
        var agents = await agentService.GetAgentsAsync(ct);
        return Ok(agents);
    }

    [HttpGet("{agentId:guid}")]
    public async ValueTask<IActionResult> GetAgentById(Guid agentId, CancellationToken ct)
    {
        var agent = await agentService.GetAgentByIdAsync(agentId, ct);
        return Ok(agent);
    }

    [HttpPost]
    public async ValueTask<IActionResult> CreateAgent([FromBody] CreateAgentRequest request, CancellationToken ct)
    {
        var result = await agentService.CreateAgentAsync(request, ct);
        return CreatedAtAction(nameof(GetAgentById), new { agentId = result.Id }, result);
    }

    [HttpPut("{agentId:guid}")]
    public async ValueTask<IActionResult> UpdateAgent(Guid agentId, [FromBody] UpdateAgentRequest request, CancellationToken ct)
    {
        await agentService.UpdateAgentAsync(agentId, request, ct);
        return NoContent();
    }

    [HttpDelete("{agentId:guid}")]
    public async ValueTask<IActionResult> DeleteAgent(Guid agentId, CancellationToken ct)
    {
        await agentService.DeleteAgentAsync(agentId, ct);
        return NoContent();
    }
}
