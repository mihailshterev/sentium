using Sentium.AgentRuntime.Core.Rag;
using Sentium.AgentRuntime.Core.Rag.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sentium.AgentRuntime.Infrastructure.Rag;

/// <summary>
/// Orchestrates the full ingestion pipeline for a single document or batch:
/// text chunking → embedding generation → vector upsert.
/// <para>
/// New ingestion sources (inventory service, log exporters, etc.) should either:
/// <list type="bullet">
///   <item>Call <see cref="IngestAsync"/> or <see cref="IngestBatchAsync"/> directly via the REST API, or</item>
///   <item>Implement <see cref="IIngestionSource"/> and call <see cref="IngestFromSourceAsync"/>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class DocumentIngestionService(
    IEmbeddingService embeddingService,
    IVectorRepository vectorRepository,
    IOptions<RagOptions> options,
    ILogger<DocumentIngestionService> logger) : IDocumentIngestionService
{
    private readonly RagOptions ragOptions = options.Value;

    public async Task IngestAsync(IngestionRequest request, string? targetCollection = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var collectionName = targetCollection ?? ragOptions.CollectionName;
        await vectorRepository.EnsureCollectionExistsAsync(collectionName, ragOptions.VectorSize, ct);

        var chunks = TextChunker.Chunk(request.Content, ragOptions.ChunkSize, ragOptions.ChunkOverlap);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Ingesting {ChunkCount} chunks from source '{Source}' ({Type})", chunks.Count, request.Source, request.SourceType);
        }

        foreach (var chunkText in chunks)
        {
            var embedding = await embeddingService.GenerateEmbeddingAsync(chunkText, ct);

            var chunk = new DocumentChunk
            {
                Content = chunkText,
                Source = request.Source,
                SourceType = request.SourceType,
                Metadata = request.Metadata ?? []
            };

            await vectorRepository.UpsertAsync(collectionName, chunk, embedding, ct);
        }
    }

    public async Task IngestBatchAsync(IEnumerable<IngestionRequest> requests, string? targetCollection = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(requests, nameof(requests));

        foreach (var request in requests)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await IngestAsync(request, targetCollection, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to ingest document from source '{Source}'. Continuing batch.", request.Source);
            }
        }
    }

    public async Task IngestFromSourceAsync(IIngestionSource source, string? targetCollection = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Starting ingestion from source '{SourceName}' ({SourceType})", source.SourceName, source.SourceType);
        }

        var count = 0;
        await foreach (var request in source.FetchDocumentsAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await IngestAsync(request, targetCollection, ct);
                count++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to ingest document {Index} from source '{SourceName}'.", count, source.SourceName);
            }
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Completed ingestion from source '{SourceName}': {Count} documents processed", source.SourceName, count);
        }
    }

    public async Task RemoveBySourceAsync(string source, string? targetCollection = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source, nameof(source));

        var collectionName = targetCollection ?? ragOptions.CollectionName;

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Removing all vectors for source '{Source}' from collection '{Collection}'", source, collectionName);
        }

        await vectorRepository.DeleteBySourceAsync(collectionName, source, ct);
    }
}
