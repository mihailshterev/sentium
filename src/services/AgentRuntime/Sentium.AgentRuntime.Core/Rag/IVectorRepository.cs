using Sentium.AgentRuntime.Core.Rag.Models;

namespace Sentium.AgentRuntime.Core.Rag;

/// <summary>
/// Abstracts over the Qdrant vector database.
/// All vector storage and retrieval operations go through this interface,
/// making it straightforward to swap the backing store in tests or future migrations.
/// </summary>
public interface IVectorRepository
{
    /// <summary>
    /// Creates the Qdrant collection if it does not already exist.
    /// Should be called once during application startup or the first ingestion.
    /// </summary>
    Task EnsureCollectionExistsAsync(string collectionName, uint vectorSize, CancellationToken ct = default);

    /// <summary>
    /// Inserts or replaces a single document chunk along with its pre-computed embedding.
    /// Uses the chunk's <see cref="DocumentChunk.Id"/> as the stable Qdrant point ID.
    /// </summary>
    Task UpsertAsync(string collectionName, DocumentChunk chunk, float[] embedding, CancellationToken ct = default);

    /// <summary>
    /// Performs a cosine-similarity search and returns the top-<paramref name="topK"/> results
    /// whose score meets or exceeds <paramref name="scoreThreshold"/>.
    /// </summary>
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(string collectionName, float[] queryEmbedding, int topK = 5, float scoreThreshold = 0.0f, CancellationToken ct = default);

    /// <summary>
    /// Deletes all points whose <c>source</c> payload field exactly matches <paramref name="source"/>.
    /// Used to remove all vectors associated with a single document or entity.
    /// </summary>
    Task DeleteBySourceAsync(string collectionName, string source, CancellationToken ct = default);
}
