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
    /// <param name="scope">
    /// Optional per-user visibility filter. When non-null, results are restricted to shared entries
    /// plus the given user's own entries; a filter with a <c>null</c> <see cref="KnowledgeScopeFilter.UserId"/>
    /// narrows this to shared/global entries only. When <c>null</c> (default), no scope filtering is
    /// applied - but all current collections (knowledge_base, agent_learnings, user_memories) pass a
    /// non-null filter, so the unfiltered branch is effectively unused.
    /// </param>
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(string collectionName, float[] queryEmbedding, int topK = 5, float scoreThreshold = 0.0f, KnowledgeScopeFilter? scope = null, CancellationToken ct = default);

    /// <summary>
    /// Deletes all points whose <c>source</c> payload field exactly matches <paramref name="source"/>.
    /// Used to remove all vectors associated with a single document or entity.
    /// </summary>
    Task DeleteBySourceAsync(string collectionName, string source, CancellationToken ct = default);

    /// <summary>
    /// Returns high-level statistics for the given collection (point count, vector size, etc.).
    /// Returns <c>null</c> if the collection does not exist.
    /// </summary>
    Task<CollectionStats?> GetCollectionStatsAsync(string collectionName, CancellationToken ct = default);

    /// <summary>
    /// Deletes the entire collection and all its points. Use with caution!
    /// </summary>
    /// <param name="collectionName">The name of the collection to delete.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteCollectionAsync(string collectionName, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a page of document chunks from a collection using Qdrant scroll.
    /// Returns up to <paramref name="limit"/> chunks, starting after <paramref name="offset"/>.
    /// </summary>
    /// <param name="scope">
    /// Optional per-user visibility filter (same semantics as <see cref="SearchAsync"/>).
    /// When <c>null</c>, no scope filtering is applied.
    /// </param>
    Task<IReadOnlyList<DocumentChunk>> GetPageAsync(string collectionName, ulong limit = 200, ulong? offset = null, KnowledgeScopeFilter? scope = null, CancellationToken ct = default);
}
