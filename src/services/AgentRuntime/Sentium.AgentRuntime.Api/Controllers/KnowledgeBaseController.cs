using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.AgentRuntime.Core.Rag;
using Sentium.AgentRuntime.Core.Rag.Models;

namespace Sentium.AgentRuntime.Api.Controllers;

/// <summary>
/// Controller for managing knowledge base collections and their contents.
/// </summary>
[ApiController]
[Authorize]
[Route("knowledge-base")]
public sealed class KnowledgeBaseController(IVectorRepository vectorRepository) : ControllerBase
{
    /// <summary>
    /// Returns knowledge-base collection statistics from the vector store.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Knowledge-base collection statistics.</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(IReadOnlyList<CollectionStats>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CollectionStats>>> GetKnowledgeBaseStats(CancellationToken ct)
    {
        var tasks = KnowledgeCollections.All.Select(c => vectorRepository.GetCollectionStatsAsync(c, ct));
        var results = await Task.WhenAll(tasks);

        var stats = results
            .Where(r => r is not null)
            .Select(r => r!)
            .ToList();

        return Ok(stats);
    }

    /// <summary>
    /// Deletes an entire collection from the vector store.
    /// </summary>
    /// <param name="collection">The name of the collection to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content if deletion is successful.</returns>
    [HttpDelete("collections/{collection}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteCollection(string collection, CancellationToken ct)
    {
        if (KnowledgeCollections.All.Contains(collection))
        {
            return Problem(detail: $"Collection {collection} is protected.", statusCode: StatusCodes.Status400BadRequest);
        }

        await vectorRepository.DeleteCollectionAsync(collection, ct);
        return NoContent();
    }
}
