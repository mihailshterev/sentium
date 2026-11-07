using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Pgvector;
using Sentium.Codex.Contracts;
using Sentium.Codex.Data;

namespace Sentium.Codex.Features.Rag;

/// <summary>
/// Re-embeds stored chunks whose vectors were produced by another embedding model than the configured
/// <c>Rag:EmbeddingModelName</c>, from their stored <c>Content</c> (no re-ingest). Driven by
/// <see cref="EmbeddingReindexService"/> one batch at a time; idempotent and resumable.
/// </summary>
public sealed class EmbeddingReindexer(
    CodexDbContext db,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    IOptions<RagOptions> ragOptions,
    ILogger<EmbeddingReindexer> logger)
{
    public const int BatchSize = 32;

    /// <summary>
    /// Counts the chunks still waiting to be re-embedded with the configured model.
    /// </summary>
    public Task<int> CountPendingAsync(CancellationToken ct = default)
    {
        var model = ragOptions.Value.EmbeddingModelName;
        return db.KnowledgeChunks.CountAsync(c => c.EmbeddingModel != model, ct);
    }

    /// <summary>
    /// Re-embeds up to <see cref="BatchSize"/> stale chunks with the configured model.
    /// </summary>
    /// <returns>The number of chunks re-embedded; 0 when none are left.</returns>
    public async Task<int> ReindexBatchAsync(CancellationToken ct = default)
    {
        var model = ragOptions.Value.EmbeddingModelName;

        var batch = await db.KnowledgeChunks
            .Where(c => c.EmbeddingModel != model)
            .OrderBy(c => c.Id)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (batch.Count == 0)
        {
            return 0;
        }

        var embeddings = await embeddingGenerator.GenerateAsync(batch.Select(c => c.Content), cancellationToken: ct);

        if (embeddings.Count != batch.Count)
        {
            throw new InvalidOperationException($"The embedding model returned {embeddings.Count} vectors for {batch.Count} chunks.");
        }

        for (var i = 0; i < batch.Count; i++)
        {
            var vector = embeddings[i].Vector;
            if (vector.Length != CodexDbContext.EmbeddingDimensions)
            {
                throw new InvalidOperationException(
                    $"Embedding model '{model}' produced {vector.Length} dimensions but the vector store expects {CodexDbContext.EmbeddingDimensions}.");
            }

            batch[i].Embedding = new Vector(vector);
            batch[i].EmbeddingModel = model;
        }

        await db.SaveChangesAsync(ct);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Re-embedded {Count} knowledge chunks with {Model}", batch.Count, model);
        }

        return batch.Count;
    }
}
