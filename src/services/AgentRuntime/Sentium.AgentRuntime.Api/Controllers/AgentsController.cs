using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Shared.Results;

namespace Sentium.AgentRuntime.Api.Controllers;

/// <summary>
/// Controller for managing agents.
/// </summary>
[ApiController]
[Authorize]
[Route("agents")]
public sealed class AgentsController(IAgentService agentService) : ControllerBase
{
    /// <summary>
    /// Returns a page of agents (newest first).
    /// </summary>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 20, max: 100).</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A paginated list of agents.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AgentResponse>), StatusCodes.Status200OK)]
    public async ValueTask<ActionResult<PagedResponse<AgentResponse>>> GetAgents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        CancellationToken ct = default)
    {
        var agents = await agentService.GetAgentsPagedAsync(page, pageSize, ct);
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
    /// <returns>Created agent details, or HTTP 409 Conflict if an agent with the same name already exists.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async ValueTask<ActionResult<AgentResponse>> CreateAgent([FromBody] CreateAgentRequest request, CancellationToken ct)
    {
        var result = await agentService.CreateAgentAsync(request, ct);
        if (result.Status == ResultStatus.Conflict)
        {
            return Conflict(new ProblemDetails { Title = "Conflict", Detail = result.Error, Status = StatusCodes.Status409Conflict });
        }

        return CreatedAtAction(nameof(GetAgentById), new { agentId = result.Value!.Id }, result.Value);
    }

    /// <summary>
    /// Updates an existing agent based on the provided configuration
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <param name="request">Agent update request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content, HTTP 404 if the agent does not exist, or HTTP 409 Conflict if the new name is taken by another agent.</returns>
    [HttpPut("{agentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async ValueTask<IActionResult> UpdateAgent(Guid agentId, [FromBody] UpdateAgentRequest request, CancellationToken ct)
    {
        var result = await agentService.UpdateAgentAsync(agentId, request, ct);
        return result.Status switch
        {
            ResultStatus.Conflict => Conflict(new ProblemDetails { Title = "Conflict", Detail = result.Error, Status = StatusCodes.Status409Conflict }),
            ResultStatus.NotFound => NotFound(),
            _ => NoContent()
        };
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
