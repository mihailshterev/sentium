using Microsoft.AspNetCore.Mvc;
using Sentium.Codex.Contracts;
using Sentium.Codex.Contracts.Models;
using Sentium.Infrastructure.Security;

namespace Sentium.Codex.Features.Ingestion;

/// <summary>
/// Administrative REST surface for pushing content into, and removing it from, the RAG knowledge base
/// (manual back-fills, re-indexing a registered <see cref="IIngestionSource"/>). Other modules ingest
/// in-process through <see cref="IDocumentIngestionService"/>, never through these endpoints.
/// Sovereign-only: a request can target any user's scope or the shared knowledge base.
/// </summary>
[ApiController]
[AuthorizeSovereign]
[Route($"{CodexRoutes.Prefix}/ingestion")]
internal sealed class IngestionController(
    IDocumentIngestionService ingestionService,
    IEnumerable<IIngestionSource> sources) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> IngestDocument([FromBody] IngestionRequest request, CancellationToken ct)
    {
        await ingestionService.IngestAsync(request, ct: ct);
        return Accepted();
    }

    /// <summary>
    /// Each document is processed independently; a failure on one does not abort the rest.
    /// </summary>
    [HttpPost("batch")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> IngestBatch([FromBody] IEnumerable<IngestionRequest> requests, CancellationToken ct)
    {
        await ingestionService.IngestBatchAsync(requests, ct: ct);
        return Accepted();
    }

    /// <summary>
    /// Runs the registered <see cref="IIngestionSource"/> with this name.
    /// </summary>
    [HttpPost("sources/{sourceName}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> IngestFromSource([FromRoute] string sourceName, [FromServices] IEnumerable<IIngestionSource> sources, CancellationToken ct)
    {
        var source = sources.FirstOrDefault(s => s.SourceName.Equals(sourceName, StringComparison.OrdinalIgnoreCase));

        if (source is null)
        {
            return Problem(detail: $"No registered ingestion source named '{sourceName}'.", statusCode: StatusCodes.Status404NotFound);
        }

        await ingestionService.IngestFromSourceAsync(source, ct: ct);
        return Accepted();
    }

    [HttpGet("sources")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetSources()
    {
        var result = sources.Select(s => new IngestionSourceResponse(s.SourceName, s.SourceType.ToString()));

        return Ok(result);
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveBySource([FromQuery] string source, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return Problem(detail: "A non-empty 'source' query parameter is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        await ingestionService.RemoveBySourceAsync(source, ct: ct);
        return NoContent();
    }
}
