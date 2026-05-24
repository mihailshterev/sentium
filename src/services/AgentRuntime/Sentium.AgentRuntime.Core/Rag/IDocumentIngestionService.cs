using Sentium.AgentRuntime.Core.Rag.Models;

namespace Sentium.AgentRuntime.Core.Rag;

/// <summary>
/// Orchestrates the full ingestion pipeline: text chunking → embedding → vector storage.
/// Consumers include the REST API, NATS event processors, and scheduled background jobs.
/// </summary>
public interface IDocumentIngestionService
{
    /// <summary>
    /// Ingests a single document, chunking and embedding it before storage.
    /// </summary>
    Task IngestAsync(IngestionRequest request, string? targetCollection = null, CancellationToken ct = default);

    /// <summary>
    /// Ingests a collection of documents sequentially.
    /// Each request is processed independently so a partial failure does not abort the batch.
    /// </summary>
    Task IngestBatchAsync(IEnumerable<IngestionRequest> requests, string? targetCollection = null, CancellationToken ct = default);

    /// <summary>
    /// Pulls all documents from a registered <see cref="IIngestionSource"/> and ingests them.
    /// Use this for bulk back-fills or periodic refresh jobs.
    /// </summary>
    Task IngestFromSourceAsync(IIngestionSource source, string? targetCollection = null, CancellationToken ct = default);

    /// <summary>
    /// Removes all vector chunks associated with the given <paramref name="source"/> identifier.
    /// Call this when a document is deleted or its agent-accessibility is revoked.
    /// </summary>
    Task RemoveBySourceAsync(string source, string? targetCollection = null, CancellationToken ct = default);
}
