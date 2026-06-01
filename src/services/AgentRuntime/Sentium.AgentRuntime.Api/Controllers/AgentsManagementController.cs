using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sentium.AgentRuntime.Api.Controllers;

/// <summary>
/// Controller for managing agents.
/// </summary>
[ApiController]
[Authorize]
[Route("agents")]
public sealed class AgentsManagementController(IAgentService agentService) : ControllerBase
{
    /// <summary>
    /// Returns a list of all agents
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of agents</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async ValueTask<ActionResult<IReadOnlyList<AgentResponse>>> GetAgents(CancellationToken ct)
    {
        var agents = await agentService.GetAgentsAsync(ct);
        return Ok(agents);
    }

    /// <summary>
    /// Returns an agent by its ID
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Agent details</returns>
    [HttpGet("{agentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<AgentResponse>> GetAgentById(Guid agentId, CancellationToken ct)
    {
        var agent = await agentService.GetAgentByIdAsync(agentId, ct);
        return agent is null ? NotFound() : Ok(agent);
    }

    /// <summary>
    /// Creates a new agent based on the provided configuration
    /// </summary>
    /// <param name="request">Agent creation request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created agent details</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async ValueTask<ActionResult<AgentResponse>> CreateAgent([FromBody] CreateAgentRequest request, CancellationToken ct)
    {
        var result = await agentService.CreateAgentAsync(request, ct);
        return CreatedAtAction(nameof(GetAgentById), new { agentId = result.Id }, result);
    }

    /// <summary>
    /// Updates an existing agent based on the provided configuration
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <param name="request">Agent update request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content</returns>
    [HttpPut("{agentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<IActionResult> UpdateAgent(Guid agentId, [FromBody] UpdateAgentRequest request, CancellationToken ct)
    {
        var updated = await agentService.UpdateAgentAsync(agentId, request, ct);
        return updated ? NoContent() : NotFound();
    }

    /// <summary>
    /// Deletes an agent by its ID. This will remove the agent and all its associated data, including sessions and learnings.
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content</returns>
    [HttpDelete("{agentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<IActionResult> DeleteAgent(Guid agentId, CancellationToken ct)
    {
        var deleted = await agentService.DeleteAgentAsync(agentId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
