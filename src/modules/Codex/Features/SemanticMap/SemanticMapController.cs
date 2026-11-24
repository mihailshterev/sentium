using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sentium.Codex.Contracts;
using Sentium.Codex.Contracts.Models;
using Sentium.Infrastructure.Security;

namespace Sentium.Codex.Features.SemanticMap;

/// <summary>
/// Data for the portal's semantic knowledge map: chunks as graph nodes, and search hits for the query-traversal
/// animation.
/// </summary>
[ApiController]
[Authorize]
[Route($"{CodexRoutes.Prefix}/knowledge-map")]
internal sealed class SemanticMapController(
    IVectorRepository vectorRepository,
    IEmbeddingService embeddingService,
    IOptions<RagOptions> ragOptions,
    ICurrentUser currentUser) : ControllerBase
{
    private const int DefaultNodeLimit = 300;
    private const int MaxNodeLimit = 500;
    private const int MaxTopK = 50;
    private const int PreviewChars = 200;

    private static readonly HashSet<string> ScopedCollections = [.. KnowledgeCollections.All];

    /// <param name="limit">Per collection, at most 500.</param>
    /// <param name="collection">All tracked collections when omitted.</param>
    [HttpGet("nodes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<KnowledgeMapResponse>> GetNodes(
        [FromQuery] int limit = DefaultNodeLimit,
        [FromQuery] string? collection = null,
        CancellationToken ct = default)
    {
        var collections = collection is not null ? [collection] : KnowledgeCollections.All;

        var safeLimit = Math.Clamp(limit, 1, MaxNodeLimit);

        var allNodes = new List<KnowledgeMapNode>();

        var userScope = new KnowledgeScopeFilter(currentUser.UserId);

        foreach (var col in collections)
        {
            var scope = ScopedCollections.Contains(col) ? userScope : null;
            var chunks = await vectorRepository.GetPageAsync(col, (ulong)safeLimit, scope: scope, ct: ct);

            allNodes.AddRange(chunks.Select(chunk => new KnowledgeMapNode
            {
                Id = chunk.Id.ToString(),
                Content = Preview(chunk.Content),
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
    /// The best <see cref="KnowledgeMapSearchRequest.TopK"/> hits across all tracked collections.
    /// </summary>
    [HttpPost("search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<KnowledgeMapSearchResponse>> Search([FromBody] KnowledgeMapSearchRequest request, CancellationToken ct)
    {
        var topK = Math.Clamp(request.TopK, 1, MaxTopK);
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
                Content = Preview(hit.Chunk.Content),
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

    private static string Preview(string content) =>
        content.Length > PreviewChars ? content[..PreviewChars] + "…" : content;
}
