using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sentium.AgentRuntime.Core.Rag;

namespace Sentium.AgentRuntime.Api.Controllers;

/// <summary>
/// Controller for managing knowledge base collections and their contents.
/// </summary>
[ApiController]
[Authorize]
[Route("knowledge-base")]
public sealed class KnowledgeBaseController(IVectorRepository vectorRepository, IOptions<RagOptions> ragOptions) : ControllerBase
{
    private static readonly string[] TrackedCollections = ["knowledge_base", "agent_learnings", "user_memories"];

    /// <summary>
    /// Returns knowledge-base collection statistics from the vector store.
    /// Returns an array — one entry per tracked collection.
    /// </summary>
    [HttpGet("stats")]
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
    /// Deletes an entire collection from the vector store.
    /// </summary>
    [HttpDelete("collections/{collection}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteCollection(string collection, CancellationToken ct)
    {
        if (TrackedCollections.Contains(collection))
        {
            return BadRequest(new { error = $"Collection {collection} is protected." });
        }

        await vectorRepository.DeleteCollectionAsync(collection, ct);
        return NoContent();
    }
}
