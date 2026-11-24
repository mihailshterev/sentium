using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Features.Agents.Runtime;

namespace Sentium.Praxis.Features.Agents;

[ApiController]
[Authorize]
[Route($"{PraxisRoutes.Prefix}/agents")]
internal sealed class AgentsController(IAgentService agentService) : ControllerBase
{
    /// <summary>
    /// Newest first.
    /// </summary>
    /// <param name="page">1-based.</param>
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
    /// <response code="404">Unknown or control agent.</response>
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
    /// <response code="404">Unknown or control agent.</response>
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

    [HttpGet("{agentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<ActionResult<AgentResponse>> GetAgentById(Guid agentId, CancellationToken ct)
    {
        var agent = await agentService.GetAgentByIdAsync(agentId, ct);
        return agent is null ? NotFound() : Ok(agent);
    }

    /// <response code="409">The name is reserved or already taken.</response>
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

    /// <response code="409">The new name is reserved or taken by another agent.</response>
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

    [HttpDelete("{agentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<IActionResult> DeleteAgent(Guid agentId, CancellationToken ct)
    {
        var deleted = await agentService.DeleteAgentAsync(agentId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
