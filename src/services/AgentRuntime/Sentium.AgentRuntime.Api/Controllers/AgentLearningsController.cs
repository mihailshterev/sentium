using Sentium.AgentRuntime.Core.Learnings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sentium.AgentRuntime.Api.Controllers;

/// <summary>
/// Exposes agent learnings and knowledge-base overview statistics.
/// </summary>
[ApiController]
[Authorize]
[Route("agent-learnings")]
public sealed class AgentLearningsController(IAgentLearningService learningService) : ControllerBase
{
    /// <summary>
    /// Returns captured learnings. Optionally filter by agent name.
    /// </summary>
    /// <param name="agentName">Optional agent name to filter learnings.</param>
    /// <param name="count">Number of learnings to return (default 50).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of captured learnings.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AgentLearningResponse>>> GetLearnings(
        [FromQuery] string? agentName,
        [FromQuery] int count = 50,
        CancellationToken ct = default)
    {
        var results = await learningService.GetLearningsAsync(agentName, count, ct);
        return Ok(results);
    }

    /// <summary>
    /// Returns aggregate statistics about captured learnings.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Aggregate statistics about captured learnings.</returns>
    [HttpGet("stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<AgentLearningStats>> GetStats(CancellationToken ct)
    {
        var stats = await learningService.GetStatsAsync(ct);
        return Ok(stats);
    }

    /// <summary>
    /// Manually captures a learning (useful for testing or admin backfills).
    /// </summary>
    /// <param name="request">The learning capture request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The captured learning.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AgentLearningResponse>> CaptureLearning([FromBody] CaptureAgentLearningRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "Content is required." });
        }

        var result = await learningService.CaptureAsync(request, ct);
        return CreatedAtAction(nameof(GetLearnings), new { }, result);
    }

    /// <summary>
    /// Deletes a learning and removes its vectors from the knowledge base.
    /// </summary>
    /// <param name="id">The ID of the learning to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content if deletion is successful.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteLearning(Guid id, CancellationToken ct)
    {
        await learningService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Updates the content and tags of an existing learning.
    /// Old vectors are removed and the learning is re-ingested with the new content.
    /// </summary>
    /// <param name="id">The ID of the learning to update.</param>
    /// <param name="request">The update request containing new content and tags.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated learning.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentLearningResponse>> UpdateLearning(Guid id, [FromBody] UpdateAgentLearningRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "Content is required." });
        }

        var result = await learningService.UpdateAsync(id, request, ct);
        return Ok(result);
    }
}
