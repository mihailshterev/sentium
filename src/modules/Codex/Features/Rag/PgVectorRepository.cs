using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Sentium.Codex.Contracts;
using Sentium.Codex.Contracts.Models;
using Sentium.Codex.Data;
using Sentium.Codex.Entities;

namespace Sentium.Codex.Features.Rag;

/// <summary>
/// Persists and retrieves document vectors in PostgreSQL (pgvector). Every <see cref="DocumentChunk"/> is one
/// <see cref="KnowledgeChunk"/> row; a logical collection is the <see cref="KnowledgeChunk.Collection"/> column.
/// </summary>
public sealed class PgVectorRepository(CodexDbContext db, IOptions<RagOptions> ragOptions, ILogger<PgVectorRepository> logger) : IVectorRepository
{
    private const int MaxPageLimit = 1000;

    public async Task UpsertAsync(string collectionName, DocumentChunk chunk, float[] embedding, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(embedding);

        if (embedding.Length != CodexDbContext.EmbeddingDimensions)
        {
            throw new ArgumentException(
                $"Embedding has {embedding.Length} dimensions but the vector store expects {CodexDbContext.EmbeddingDimensions}. " +
                "Check that the configured embedding model matches the codex schema.",
                nameof(embedding));
        }

        var entity = await db.KnowledgeChunks.FindAsync([chunk.Id], ct);
        if (entity is null)
        {
            entity = new KnowledgeChunk
            {
                Id = chunk.Id,
                Collection = collectionName,
                Content = chunk.Content,
                Source = chunk.Source,
                Embedding = new Vector(embedding)
            };
            db.KnowledgeChunks.Add(entity);
        }

        entity.Collection = collectionName;
        entity.Content = chunk.Content;
        entity.Source = chunk.Source;
        entity.SourceType = chunk.SourceType;
        entity.Scope = chunk.Scope;
        entity.UserId = chunk.UserId;
        entity.Metadata = new Dictionary<string, string>(chunk.Metadata);
        entity.CreatedAt = chunk.CreatedAt;
        entity.Embedding = new Vector(embedding);
        entity.EmbeddingModel = ragOptions.Value.EmbeddingModelName;

        await db.SaveChangesAsync(ct);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Upserted chunk {Id} from source '{Source}' into collection '{Collection}'", chunk.Id, chunk.Source, collectionName);
        }
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(string collectionName, float[] queryEmbedding, int topK = 5, float scoreThreshold = 0.0f, KnowledgeScopeFilter? scope = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(queryEmbedding);

        if (topK <= 0)
        {
            return [];
        }

        var query = new Vector(queryEmbedding);

        // The HNSW index is scanned before the collection/scope filter is applied; iterative scan keeps
        // walking the graph until topK rows survive the filter. SET LOCAL needs a transaction, and the
        // retrying execution strategy needs that transaction wrapped so it can replay it as a unit.
        var strategy = db.Database.CreateExecutionStrategy();
        var hits = await strategy.ExecuteAsync(async cancellationToken =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Database.ExecuteSqlRawAsync("SET LOCAL hnsw.iterative_scan = relaxed_order", cancellationToken);

            var rows = await ApplyScope(db.KnowledgeChunks.AsNoTracking().Where(e => e.Collection == collectionName), scope)
                .Select(e => new { Chunk = e, Distance = e.Embedding.CosineDistance(query) })
                .OrderBy(x => x.Distance)
                .Take(topK)
                .ToListAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return rows;
        }, ct);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Vector search in '{Collection}' returned {Count} hits (topK={TopK})", collectionName, hits.Count, topK);
        }

        // relaxed_order can return neighbours slightly out of order, so re-sort after scoring.
        return hits
            .Select(hit => new VectorSearchResult
            {
                Score = (float)(1 - hit.Distance),
                Chunk = ToDocumentChunk(hit.Chunk)
            })
            .Where(result => result.Score >= scoreThreshold)
            .OrderByDescending(result => result.Score)
            .ToList();
    }

    public async Task DeleteBySourceAsync(string collectionName, string source, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var deleted = await db.KnowledgeChunks
            .Where(e => e.Collection == collectionName && e.Source == source)
            .ExecuteDeleteAsync(ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Deleted {Count} vectors with source '{Source}' from collection '{Collection}'", deleted, source, collectionName);
        }
    }

    public async Task<CollectionStats?> GetCollectionStatsAsync(string collectionName, CancellationToken ct = default)
    {
        var count = await db.KnowledgeChunks.LongCountAsync(e => e.Collection == collectionName, ct);

        return count == 0
            ? null
            : new CollectionStats(collectionName, count, CodexDbContext.EmbeddingDimensions, CodexDbContext.DistanceMetric);
    }

    public async Task DeleteCollectionAsync(string collectionName, CancellationToken ct = default)
    {
        var deleted = await db.KnowledgeChunks
            .Where(e => e.Collection == collectionName)
            .ExecuteDeleteAsync(ct);

        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning("Deleted collection '{Collection}' ({Count} vectors)", collectionName, deleted);
        }
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetPageAsync(string collectionName, ulong limit = 200, ulong? offset = null, KnowledgeScopeFilter? scope = null, CancellationToken ct = default)
    {
        var take = (int)Math.Clamp(limit, 1UL, MaxPageLimit);
        var skip = (int)Math.Min(offset ?? 0, int.MaxValue);

        var rows = await ApplyScope(db.KnowledgeChunks.AsNoTracking().Where(e => e.Collection == collectionName), scope)
            .OrderBy(e => e.CreatedAt)
            .ThenBy(e => e.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return rows.Select(ToDocumentChunk).ToList();
    }

    /// <summary>
    /// Shared entries plus the scoped user's own entries; a filter without a user narrows to shared entries
    /// only, and no filter at all disables scoping.
    /// </summary>
    private static IQueryable<KnowledgeChunk> ApplyScope(IQueryable<KnowledgeChunk> query, KnowledgeScopeFilter? scope)
    {
        if (scope is null)
        {
            return query;
        }

        if (scope.UserId is { } userId)
        {
            return query.Where(e => e.Scope == KnowledgeScope.Shared || e.UserId == userId);
        }

        return query.Where(e => e.Scope == KnowledgeScope.Shared);
    }

    private static DocumentChunk ToDocumentChunk(KnowledgeChunk entity) => new()
    {
        Id = entity.Id,
        Content = entity.Content,
        Source = entity.Source,
        SourceType = entity.SourceType,
        CreatedAt = entity.CreatedAt,
        Metadata = entity.Metadata,
        Scope = entity.Scope,
        UserId = entity.UserId
    };
}
