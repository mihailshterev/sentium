using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;
using Sentium.Infrastructure.Security;

namespace Sentium.Praxis.Controllers;

/// <summary>
/// Controller for managing agents.
/// </summary>
[ApiController]
[Authorize]
[Route($"{PraxisRoutes.Prefix}/agents")]
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
    /// The agents the signed-in user can work with: the built-in specialists and their own agents. Control roles
    /// that only steer squads (planner, reviewer, summariser) are not listed.
    /// </summary>
    [HttpGet("roster")]
    [ProducesResponseType(typeof(IReadOnlyList<AgentRosterEntry>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoster([FromServices] IAgentRoster roster, [FromServices] ICurrentUser currentUser, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(currentUser);

        return Ok(await roster.GetAsync(currentUser.UserId, ct));
    }

    /// <summary>
    /// The built-in specialists with their code-defined and effective tools for the signed-in user.
    /// </summary>
    [HttpGet("built-in")]
    [ProducesResponseType(typeof(IReadOnlyList<BuiltInAgentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetBuiltInAgents([FromServices] IBuiltInAgentService builtInAgents, [FromServices] ICurrentUser currentUser, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(builtInAgents);
        ArgumentNullException.ThrowIfNull(currentUser);

        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await builtInAgents.GetAsync(userId, ct));
    }

    /// <summary>
    /// Replaces a built-in agent's tools for the signed-in user (<c>tools: null</c> = every tool).
    /// </summary>
    /// <param name="agentName">The built-in agent's name.</param>
    /// <param name="request">The tool names.</param>
    /// <param name="builtInAgents">The built-in agent service.</param>
    /// <param name="currentUser">The signed-in user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated agent, or HTTP 404 for an unknown or control agent.</returns>
    [HttpPut("built-in/{agentName}/tools")]
    [ProducesResponseType(typeof(BuiltInAgentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetBuiltInAgentTools(
        string agentName,
        [FromBody] UpdateAgentToolsRequest request,
        [FromServices] IBuiltInAgentService builtInAgents,
        [FromServices] ICurrentUser currentUser,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(builtInAgents);
        ArgumentNullException.ThrowIfNull(currentUser);

        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await builtInAgents.SetToolsAsync(agentName, userId, request.Tools, ct);
        return result.Status == ResultStatus.NotFound ? NotFound() : Ok(result.Value);
    }

    /// <summary>
    /// Restores a built-in agent's code-defined tools for the signed-in user.
    /// </summary>
    /// <param name="agentName">The built-in agent's name.</param>
    /// <param name="builtInAgents">The built-in agent service.</param>
    /// <param name="currentUser">The signed-in user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The agent with its default tools, or HTTP 404 for an unknown or control agent.</returns>
    [HttpDelete("built-in/{agentName}/tools")]
    [ProducesResponseType(typeof(BuiltInAgentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetBuiltInAgentTools(
        string agentName,
        [FromServices] IBuiltInAgentService builtInAgents,
        [FromServices] ICurrentUser currentUser,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(builtInAgents);
        ArgumentNullException.ThrowIfNull(currentUser);

        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await builtInAgents.ResetToolsAsync(agentName, userId, ct);
        return result.Status == ResultStatus.NotFound ? NotFound() : Ok(result.Value);
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
