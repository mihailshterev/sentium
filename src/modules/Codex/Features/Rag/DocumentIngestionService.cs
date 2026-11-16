using System.Runtime.CompilerServices;
using Sentium.Codex.Contracts;
using Sentium.Codex.Contracts.Models;
using Microsoft.Extensions.Options;

namespace Sentium.Codex.Features.Rag;

/// <summary>
/// Orchestrates the full ingestion pipeline for a single document or batch:
/// text chunking → embedding generation → vector upsert.
/// <para>
/// New ingestion sources should either:
/// <list type="bullet">
///   <item>Call <see cref="IngestAsync"/> or <see cref="IngestBatchAsync"/> directly via the REST API, or</item>
///   <item>Implement <see cref="IIngestionSource"/> and call <see cref="IngestFromSourceAsync"/>.</item>
/// </list>
/// </para>
/// </summary>
internal sealed class DocumentIngestionService(
    IEmbeddingService embeddingService,
    IVectorRepository vectorRepository,
    IOptions<RagOptions> options,
    ILogger<DocumentIngestionService> logger) : IDocumentIngestionService
{
    /// <summary>
    /// Matches <c>EmbeddingReindexer.BatchSize</c>: small enough for the embedding server's batch, large enough to
    /// amortize the round trip.
    /// </summary>
    private const int EmbeddingBatchSize = 32;

    private readonly RagOptions ragOptions = options.Value;

    public async Task IngestAsync(IngestionRequest request, string? targetCollection = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var collectionName = targetCollection ?? ragOptions.CollectionName;

        await foreach (var batch in EmbedAsync(request, ct))
        {
            await vectorRepository.AddRangeAsync(collectionName, batch, ct);
        }
    }

    public async Task ReplaceAsync(IngestionRequest request, string documentKey, string? targetCollection = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(documentKey);

        if (request.Metadata?.TryGetValue(documentKey, out var documentValue) != true || string.IsNullOrEmpty(documentValue))
        {
            throw new ArgumentException($"The request metadata has no '{documentKey}' to identify the document by.", nameof(documentKey));
        }

        var collectionName = targetCollection ?? ragOptions.CollectionName;

        await vectorRepository.ReplaceByMetadataAsync(collectionName, documentKey, documentValue, EmbedAsync(request, ct), ct);
    }

    public async Task RemoveDocumentAsync(string documentKey, string documentValue, string? targetCollection = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentValue);

        var collectionName = targetCollection ?? ragOptions.CollectionName;
        var deleted = await vectorRepository.DeleteByMetadataAsync(collectionName, documentKey, documentValue, ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Removed {Count} chunks of document {Key}={Value} from collection '{Collection}'", deleted, documentKey, documentValue, collectionName);
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

    /// <summary>
    /// Chunks the request and embeds the chunks a batch per request to the embedding server, so a large document
    /// never holds all of its vectors in memory at once.
    /// </summary>
    private async IAsyncEnumerable<IReadOnlyList<EmbeddedChunk>> EmbedAsync(IngestionRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        var chunks = TextChunker.Chunk(request.Content, ragOptions.ChunkSize, ragOptions.ChunkOverlap);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Ingesting {ChunkCount} chunks from source '{Source}' ({Type})", chunks.Count, request.Source, request.SourceType);
        }

        var metadata = request.Metadata ?? [];

        foreach (var texts in chunks.Chunk(EmbeddingBatchSize))
        {
            var embeddings = await embeddingService.GenerateEmbeddingsAsync(texts, ct);

            yield return [.. texts.Select((text, i) => new EmbeddedChunk(
                new DocumentChunk
                {
                    Content = text,
                    Source = request.Source,
                    SourceType = request.SourceType,
                    Metadata = metadata,
                    Scope = request.Scope,
                    UserId = request.UserId
                },
                embeddings[i]))];
        }
    }
}
