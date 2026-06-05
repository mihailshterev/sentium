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
    private const uint MaxPageLimit = 1000;

    private const string FieldContent = "content";
    private const string FieldSource = "source";
    private const string FieldSourceType = "source_type";
    private const string FieldCreatedAt = "created_at";
    private const string FieldScope = "scope";
    private const string FieldUserId = "user_id";
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
        point.Payload[FieldScope] = chunk.Scope;

        if (chunk.UserId.HasValue)
        {
            point.Payload[FieldUserId] = chunk.UserId.Value.ToString();
        }

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

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(string collectionName, float[] queryEmbedding, int topK = 5, float scoreThreshold = 0.0f, KnowledgeScopeFilter? scope = null, CancellationToken ct = default)
    {
        var hits = await qdrantClient.SearchAsync(
            collectionName,
            queryEmbedding,
            filter: BuildScopeFilter(scope),
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

    public async Task DeleteBySourceAsync(string collectionName, string source, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source, nameof(source));

        var filter = new Filter();
        filter.Must.Add(Conditions.MatchKeyword(FieldSource, source));

        await qdrantClient.DeleteAsync(collectionName, filter, cancellationToken: ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Deleted all vectors with source '{Source}' from collection '{Collection}'", source, collectionName);
        }
    }

    private static Filter? BuildScopeFilter(KnowledgeScopeFilter? scope)
    {
        if (scope is null)
        {
            return null;
        }

        var filter = new Filter();
        filter.Should.Add(Conditions.MatchKeyword(FieldScope, KnowledgeScope.Shared));
        filter.Should.Add(Conditions.IsEmpty(FieldScope));

        if (scope.UserId.HasValue)
        {
            filter.Should.Add(Conditions.MatchKeyword(FieldUserId, scope.UserId.Value.ToString()));
        }

        return filter;
    }

    private static DocumentChunk ReconstructChunk(ScoredPoint hit) => BuildChunk(hit.Id, hit.Payload);

    private static DocumentChunk BuildChunk(PointId pointId, Google.Protobuf.Collections.MapField<string, Value> payload)
    {
        var id = pointId.HasUuid ? Guid.Parse(pointId.Uuid) : Guid.Empty;

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

        var scope = payload.TryGetValue(FieldScope, out var scopeVal) && !string.IsNullOrEmpty(scopeVal.StringValue)
            ? scopeVal.StringValue
            : KnowledgeScope.Shared;

        Guid? userId = payload.TryGetValue(FieldUserId, out var uidVal) && Guid.TryParse(uidVal.StringValue, out var parsedUid)
            ? parsedUid
            : null;

        return new DocumentChunk
        {
            Id = id,
            Content = payload.TryGetValue(FieldContent, out var c) ? c.StringValue : string.Empty,
            Source = payload.TryGetValue(FieldSource, out var s) ? s.StringValue : string.Empty,
            SourceType = sourceType,
            CreatedAt = createdAt,
            Metadata = metadata,
            Scope = scope,
            UserId = userId
        };
    }

    public async Task<CollectionStats?> GetCollectionStatsAsync(string collectionName, CancellationToken ct = default)
    {
        var exists = await qdrantClient.CollectionExistsAsync(collectionName, ct);
        if (!exists)
        {
            return null;
        }

        var info = await qdrantClient.GetCollectionInfoAsync(collectionName, ct);

        var vectorSize = info.Config?.Params?.VectorsConfig?.Params?.Size ?? 0;
        var distance = info.Config?.Params?.VectorsConfig?.Params?.Distance.ToString() ?? "Cosine";

        return new CollectionStats(
            collectionName,
            (long)info.PointsCount,
            (uint)vectorSize,
            distance);
    }

    public async Task DeleteCollectionAsync(string collectionName, CancellationToken ct = default)
    {
        var exists = await qdrantClient.CollectionExistsAsync(collectionName, ct);
        if (!exists)
        {
            return;
        }

        await qdrantClient.DeleteCollectionAsync(collectionName, cancellationToken: ct);

        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning("Deleted Qdrant collection '{Collection}'", collectionName);
        }
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetPageAsync(string collectionName, ulong limit = 200, ulong? offset = null, KnowledgeScopeFilter? scope = null, CancellationToken ct = default)
    {
        var exists = await qdrantClient.CollectionExistsAsync(collectionName, ct);
        if (!exists)
        {
            return [];
        }

        var requestedLimit = limit == 0 ? 1 : limit;

        var safeLimit = (uint)Math.Min(requestedLimit, MaxPageLimit);

        var offsetId = offset.HasValue ? new PointId { Num = offset.Value } : null;

        var result = await qdrantClient.ScrollAsync(
            collectionName,
            BuildScopeFilter(scope),
            safeLimit,
            offsetId,
            true,
            false,
            cancellationToken: ct);

        return result.Result
            .Select(point => BuildChunk(point.Id, point.Payload))
            .ToList();
    }
}
