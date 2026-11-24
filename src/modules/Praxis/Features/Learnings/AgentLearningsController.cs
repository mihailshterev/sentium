using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;

namespace Sentium.Praxis.Features.Learnings;

[ApiController]
[Authorize]
[Route($"{PraxisRoutes.Prefix}/agent-learnings")]
internal sealed class AgentLearningsController(IAgentLearningService learningService) : ControllerBase
{
    /// <summary>
    /// Newest first.
    /// </summary>
    /// <param name="page">1-based.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AgentLearningResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AgentLearningResponse>>> GetLearnings(
        [FromQuery] string? agentName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        CancellationToken ct = default)
    {
        var results = await learningService.GetLearningsAsync(agentName, page, pageSize, ct);
        return Ok(results);
    }

    [HttpGet("stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<AgentLearningStats>> GetStats(CancellationToken ct)
    {
        var stats = await learningService.GetStatsAsync(ct);
        return Ok(stats);
    }

    /// <summary>
    /// Deletes a learning and removes its vectors from the knowledge base.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLearning(Guid id, CancellationToken ct)
    {
        var deleted = await learningService.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>
    /// Replaces the content and tags, and re-ingests the learning in place of its old vectors.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentLearningResponse>> UpdateLearning(Guid id, [FromBody] UpdateAgentLearningRequest request, CancellationToken ct)
    {
        var result = await learningService.UpdateAsync(id, request, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
