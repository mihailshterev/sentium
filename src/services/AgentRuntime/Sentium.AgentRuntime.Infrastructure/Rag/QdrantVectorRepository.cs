using Sentium.AgentRuntime.Core.Rag;
using Sentium.AgentRuntime.Core.Rag.Models;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Sentium.AgentRuntime.Infrastructure.Rag;

/// <summary>
/// Persists and retrieves document vectors using Qdrant.
/// Each <see cref="DocumentChunk"/> is stored as a Qdrant point whose payload mirrors
/// all chunk fields so that results can be fully reconstructed without a secondary lookup.
/// </summary>
public sealed class QdrantVectorRepository(QdrantClient qdrantClient, ILogger<QdrantVectorRepository> logger) : IVectorRepository
{
    private const string FieldContent = "content";
    private const string FieldSource = "source";
    private const string FieldSourceType = "source_type";
    private const string FieldCreatedAt = "created_at";
    private const string MetadataPrefix = "meta_";

    public async Task EnsureCollectionExistsAsync(string collectionName, uint vectorSize, CancellationToken ct = default)
    {
        var exists = await qdrantClient.CollectionExistsAsync(collectionName, ct);
        if (exists)
        {
            return;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Creating Qdrant collection '{Collection}' with vector size {Size}", collectionName, vectorSize);
        }

        await qdrantClient.CreateCollectionAsync(
            collectionName,
            new VectorParams { Size = vectorSize, Distance = Distance.Cosine },
            cancellationToken: ct);
    }

    public async Task UpsertAsync(string collectionName, DocumentChunk chunk, float[] embedding, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(chunk, nameof(chunk));

        var point = new PointStruct
        {
            Id = chunk.Id,
            Vectors = embedding,
        };

        point.Payload[FieldContent] = chunk.Content;
        point.Payload[FieldSource] = chunk.Source;
        point.Payload[FieldSourceType] = chunk.SourceType.ToString();
        point.Payload[FieldCreatedAt] = chunk.CreatedAt.ToString("O");

        foreach (var (key, value) in chunk.Metadata)
        {
            point.Payload[$"{MetadataPrefix}{key}"] = value;
        }

        await qdrantClient.UpsertAsync(collectionName, [point], cancellationToken: ct);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Upserted chunk {Id} from source '{Source}' into collection '{Collection}'", chunk.Id, chunk.Source, collectionName);
        }
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(string collectionName, float[] queryEmbedding, int topK = 5, float scoreThreshold = 0.0f, CancellationToken ct = default)
    {
        var hits = await qdrantClient.SearchAsync(
            collectionName,
            queryEmbedding,
            limit: (ulong)topK,
            scoreThreshold: scoreThreshold,
            cancellationToken: ct);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Vector search in '{Collection}' returned {Count} hits (topK={TopK})", collectionName, hits.Count, topK);
        }

        return hits
            .Select(hit => new VectorSearchResult
            {
                Score = hit.Score,
                Chunk = ReconstructChunk(hit)
            })
            .ToList();
    }

    private static DocumentChunk ReconstructChunk(ScoredPoint hit)
    {
        var payload = hit.Payload;

        var id = hit.Id.HasUuid
            ? Guid.Parse(hit.Id.Uuid)
            : Guid.Empty;

        var metadata = payload
            .Where(kvp => kvp.Key.StartsWith(MetadataPrefix, StringComparison.Ordinal))
            .ToDictionary(kvp => kvp.Key[MetadataPrefix.Length..], kvp => kvp.Value.StringValue);

        var sourceType = payload.TryGetValue(FieldSourceType, out var stVal)
            && Enum.TryParse<IngestionSourceType>(stVal.StringValue, out var parsed)
            ? parsed
            : IngestionSourceType.Custom;

        var createdAt = payload.TryGetValue(FieldCreatedAt, out var dtVal)
            && DateTimeOffset.TryParse(dtVal.StringValue, out var parsedDt)
            ? parsedDt
            : DateTimeOffset.UtcNow;

        return new DocumentChunk
        {
            Id = id,
            Content = payload.TryGetValue(FieldContent, out var c) ? c.StringValue : string.Empty,
            Source = payload.TryGetValue(FieldSource, out var s) ? s.StringValue : string.Empty,
            SourceType = sourceType,
            CreatedAt = createdAt,
            Metadata = metadata
        };
    }
}
