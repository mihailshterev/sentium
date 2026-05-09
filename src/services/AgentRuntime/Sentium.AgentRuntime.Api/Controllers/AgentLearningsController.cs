using Sentium.AgentRuntime.Core.Learnings;
using Sentium.AgentRuntime.Core.Rag;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Sentium.AgentRuntime.Api.Controllers;

/// <summary>
/// Exposes agent learnings and knowledge-base overview statistics.
/// </summary>
[ApiController]
[Authorize]
[Route("agent-learnings")]
public sealed class AgentLearningsController(
    IAgentLearningService learningService,
    IVectorRepository vectorRepository,
    IOptions<RagOptions> ragOptions) : ControllerBase
{
    /// <summary>
    /// Returns captured learnings. Optionally filter by agent name.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLearnings([FromQuery] string? agentName, [FromQuery] int count = 50, CancellationToken ct = default)
    {
        var results = await learningService.GetLearningsAsync(agentName, count, ct);
        return Ok(results);
    }

    /// <summary>
    /// Returns aggregate statistics about captured learnings.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var stats = await learningService.GetStatsAsync(ct);
        return Ok(stats);
    }

    /// <summary>
    /// Returns knowledge-base collection statistics from the vector store.
    /// Returns an array — one entry per tracked collection.
    /// </summary>
    [HttpGet("knowledge-base/stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKnowledgeBaseStats(CancellationToken ct)
    {
        var collections = new[]
        {
            ragOptions.Value.CollectionName,
            "agent_learnings",
            "user_memories"
        };

        var tasks = collections.Select(c => vectorRepository.GetCollectionStatsAsync(c, ct));
        var results = await Task.WhenAll(tasks);

        var stats = results
            .Where(r => r is not null)
            .Select(r => new
            {
                collectionName = r!.CollectionName,
                pointCount = r.PointCount,
                vectorSize = r.VectorSize,
                distanceMetric = r.DistanceMetric
            })
            .ToList();

        return Ok(stats);
    }

    /// <summary>
    /// Manually captures a learning (useful for testing or admin backfills).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CaptureLearning([FromBody] CaptureAgentLearningRequest request, CancellationToken ct)
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
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLearning(Guid id, [FromBody] UpdateAgentLearningRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "Content is required." });
        }

        try
        {
            var result = await learningService.UpdateAsync(id, request, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Learning {id} not found." });
        }
    }
}
