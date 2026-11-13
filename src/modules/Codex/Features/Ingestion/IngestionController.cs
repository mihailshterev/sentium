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
public sealed class IngestionController(
    IDocumentIngestionService ingestionService,
    IEnumerable<IIngestionSource> sources) : ControllerBase
{
    /// <summary>
    /// Ingest a single document into the knowledge base.
    /// The service will chunk, embed, and store it in the vector store.
    /// </summary>
    /// <param name="request">The request containing the document to ingest.</param>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> IngestDocument([FromBody] IngestionRequest request, CancellationToken ct)
    {
        await ingestionService.IngestAsync(request, ct: ct);
        return Accepted();
    }

    /// <summary>
    /// Ingest multiple documents in a single call.
    /// Each document is processed independently; a failure on one does not abort the rest.
    /// </summary>
    /// <param name="requests">The requests containing the documents to ingest.</param>
    [HttpPost("batch")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> IngestBatch([FromBody] IEnumerable<IngestionRequest> requests, CancellationToken ct)
    {
        await ingestionService.IngestBatchAsync(requests, ct: ct);
        return Accepted();
    }

    /// <summary>
    /// Triggers ingestion from a named registered <see cref="IIngestionSource"/>.
    /// Useful for on-demand back-fills or scheduled refresh jobs.
    /// Returns 404 if no source with that name is registered.
    /// </summary>
    /// <param name="sourceName">The name of the ingestion source to trigger.</param>
    /// <param name="sources">Injected enumeration of all registered ingestion sources.</param>
    /// <returns>Accepted if the source was found and ingestion started; 404 otherwise.</returns>
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

    /// <summary>
    /// Lists all named ingestion sources currently registered in the DI container.
    /// </summary>
    [HttpGet("sources")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetSources()
    {
        var result = sources.Select(s => new IngestionSourceResponse(s.SourceName, s.SourceType.ToString()));

        return Ok(result);
    }

    /// <summary>
    /// Removes all vector chunks associated with the given source identifier.
    /// </summary>
    /// <param name="source">The source identifier whose vectors should be removed.</param>
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
