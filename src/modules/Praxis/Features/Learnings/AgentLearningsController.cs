using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;

namespace Sentium.Praxis.Features.Learnings;

/// <summary>
/// Exposes agent learnings and knowledge-base overview statistics.
/// </summary>
[ApiController]
[Authorize]
[Route($"{PraxisRoutes.Prefix}/agent-learnings")]
internal sealed class AgentLearningsController(IAgentLearningService learningService) : ControllerBase
{
    /// <summary>
    /// Returns a page of captured learnings (newest first). Optionally filter by agent name.
    /// </summary>
    /// <param name="agentName">Optional agent name to filter learnings.</param>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Number of learnings per page (default: 20, max: 100).</param>
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

    /// <summary>
    /// Returns aggregate statistics about captured learnings.
    /// </summary>
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
    /// Updates the content and tags of an existing learning.
    /// Old vectors are removed and the learning is re-ingested with the new content.
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
