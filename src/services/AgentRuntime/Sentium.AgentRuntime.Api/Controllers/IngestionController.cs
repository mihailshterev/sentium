using Sentium.AgentRuntime.Core.Rag;
using Sentium.AgentRuntime.Core.Rag.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sentium.AgentRuntime.Api.Controllers;

/// <summary>
/// REST surface for pushing content into the RAG knowledge base.
/// Downstream services call these endpoints to contribute their data without requiring direct access to the vector store.
/// These endpoints are internal-only — external access is gated by the API gateway.
/// </summary>
[ApiController]
[Route("ingestion")]
public sealed class IngestionController(
    IDocumentIngestionService ingestionService,
    IEnumerable<IIngestionSource> sources) : ControllerBase
{
    /// <summary>
    /// Ingest a single document into the knowledge base.
    /// The service will chunk, embed, and store it in Qdrant.
    /// </summary>
    /// <param name="request">The request containing the document to ingest.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Accepted response.</returns>
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
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Accepted response.</returns>
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
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Accepted if the source was found and ingestion started; 404 otherwise.</returns>
    [HttpPost("sources/{sourceName}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> IngestFromSource([FromRoute] string sourceName, [FromServices] IEnumerable<IIngestionSource> sources, CancellationToken ct)
    {
        var source = sources.FirstOrDefault(s => s.SourceName.Equals(sourceName, StringComparison.OrdinalIgnoreCase));

        if (source is null)
        {
            return NotFound(new { error = $"No registered ingestion source named '{sourceName}'." });
        }

        await ingestionService.IngestFromSourceAsync(source, ct: ct);
        return Accepted();
    }

    /// <summary>
    /// Lists all named ingestion sources currently registered in the DI container.
    /// </summary>
    /// <returns>List of ingestion sources with their names and types.</returns>
    [HttpGet("sources")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetSources()
    {
        var result = sources.Select(s => new
        {
            name = s.SourceName,
            type = s.SourceType.ToString()
        });

        return Ok(result);
    }

    /// <summary>
    /// Removes all vector chunks associated with the given source identifier.
    /// Called by source services when a document is deleted or its agent-accessibility is revoked.
    /// </summary>
    /// <param name="source">The source identifier whose vectors should be removed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content if the operation is successful </returns>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveBySource([FromQuery] string source, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return BadRequest(new { error = "A non-empty 'source' query parameter is required." });
        }

        await ingestionService.RemoveBySourceAsync(source, ct: ct);
        return NoContent();
    }
}
