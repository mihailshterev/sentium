using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Rag;
using Sentium.AgentRuntime.Core.Rag.Models;
using Sentium.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Sentium.AgentRuntime.Api.Controllers;

/// <summary>
/// Provides graph-ready data for the semantic knowledge map visualization.
/// Returns nodes (document chunks) from one or more Qdrant collections and
/// supports semantic search for query-traversal animation.
/// </summary>
[ApiController]
[Authorize]
[Route("knowledge-map")]
public sealed class SemanticMapController(
    IVectorRepository vectorRepository,
    IEmbeddingService embeddingService,
    IOptions<RagOptions> ragOptions,
    ICurrentUser currentUser) : ControllerBase
{
    private static readonly HashSet<string> ScopedCollections = ["knowledge_base", "agent_learnings", "user_memories"];

    /// <summary>
    /// Returns a page of nodes from the specified collections for graph rendering.
    /// Each node represents a document chunk with its source metadata.
    /// </summary>
    /// <param name="limit">The maximum number of nodes to return (default 300, max 500).</param>
    /// <param name="collection">Optional specific collection to query; if not provided, queries all tracked collections.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of knowledge map nodes with their metadata.</returns>
    [HttpGet("nodes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<KnowledgeMapResponse>> GetNodes(
        [FromQuery] int limit = 300,
        [FromQuery] string? collection = null,
        CancellationToken ct = default)
    {
        var collections = collection is not null ? [collection] : KnowledgeCollections.All;

        var safeLimit = Math.Clamp(limit, 1, 500);

        var allNodes = new List<KnowledgeMapNode>();

        var userScope = new KnowledgeScopeFilter(currentUser.UserId);

        foreach (var col in collections)
        {
            var scope = ScopedCollections.Contains(col) ? userScope : null;
            var chunks = await vectorRepository.GetPageAsync(col, (ulong)safeLimit, scope: scope, ct: ct);

            allNodes.AddRange(chunks.Select(chunk => new KnowledgeMapNode
            {
                Id = chunk.Id.ToString(),
                Content = chunk.Content.Length > 200 ? chunk.Content[..200] + "…" : chunk.Content,
                FullContent = chunk.Content,
                Source = chunk.Source,
                SourceType = chunk.SourceType.ToString(),
                Collection = col,
                CreatedAt = chunk.CreatedAt,
                Metadata = chunk.Metadata
            }));
        }

        return Ok(new KnowledgeMapResponse
        {
            Nodes = allNodes,
            TotalNodes = allNodes.Count,
            Collections = collections
        });
    }

    /// <summary>
    /// Performs a semantic search across all tracked collections and returns
    /// ranked results for query-traversal visualization.
    /// </summary>
    /// <param name="request">The search request containing the query and top-K parameter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of search results with their scores and metadata.</returns>
    [HttpPost("search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<KnowledgeMapSearchResponse>> Search([FromBody] KnowledgeMapSearchRequest request, CancellationToken ct)
    {
        var topK = Math.Clamp(request.TopK, 1, 50);
        var threshold = ragOptions.Value.ScoreThreshold;

        float[] embedding;
        try
        {
            embedding = await embeddingService.GenerateEmbeddingAsync(request.Query, ct);
        }
        catch
        {
            return Problem(detail: "Embedding service is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var allResults = new List<KnowledgeMapSearchResult>();

        var userScope = new KnowledgeScopeFilter(currentUser.UserId);

        foreach (var col in KnowledgeCollections.All)
        {
            var scope = ScopedCollections.Contains(col) ? userScope : null;
            var hits = await vectorRepository.SearchAsync(col, embedding, topK, threshold, scope: scope, ct: ct);

            allResults.AddRange(hits.Select(hit => new KnowledgeMapSearchResult
            {
                Id = hit.Chunk.Id.ToString(),
                Score = hit.Score,
                Content = hit.Chunk.Content.Length > 200 ? hit.Chunk.Content[..200] + "…" : hit.Chunk.Content,
                FullContent = hit.Chunk.Content,
                Source = hit.Chunk.Source,
                SourceType = hit.Chunk.SourceType.ToString(),
                Collection = col,
                CreatedAt = hit.Chunk.CreatedAt
            }));
        }

        var ranked = allResults
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();

        return Ok(new KnowledgeMapSearchResponse
        {
            Query = request.Query,
            Results = ranked,
            TotalMatches = ranked.Count
        });
    }
}
