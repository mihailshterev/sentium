using Sentium.Codex.Contracts.Models;

namespace Sentium.Codex.Contracts;

/// <summary>
/// The vector store (PostgreSQL + pgvector, <c>codex</c> schema). A "collection" is a logical partition of the store
/// (see <see cref="KnowledgeCollections"/>); it needs no setup.
/// </summary>
public interface IVectorRepository
{
    /// <summary>
    /// Keyed by <see cref="DocumentChunk.Id"/>.
    /// </summary>
    Task UpsertAsync(string collectionName, DocumentChunk chunk, float[] embedding, CancellationToken ct = default);

    /// <summary>
    /// Inserts new chunks (their ids must not exist yet) in one round trip.
    /// </summary>
    Task AddRangeAsync(string collectionName, IReadOnlyList<EmbeddedChunk> chunks, CancellationToken ct = default);

    /// <summary>
    /// In one transaction, deletes every entry whose metadata has <paramref name="metadataKey"/> =
    /// <paramref name="metadataValue"/> and inserts the streamed <paramref name="batches"/>, so a document is
    /// either fully replaced or left as it was, and re-ingesting it never duplicates its chunks.
    /// </summary>
    Task ReplaceByMetadataAsync(string collectionName, string metadataKey, string metadataValue, IAsyncEnumerable<IReadOnlyList<EmbeddedChunk>> batches, CancellationToken ct = default);

    /// <summary>
    /// Deletes every entry whose metadata has <paramref name="metadataKey"/> = <paramref name="metadataValue"/>.
    /// </summary>
    /// <returns>The number of deleted entries.</returns>
    Task<int> DeleteByMetadataAsync(string collectionName, string metadataKey, string metadataValue, CancellationToken ct = default);

    /// <summary>
    /// The top <paramref name="topK"/> cosine-similarity hits scoring at least <paramref name="scoreThreshold"/>.
    /// </summary>
    /// <param name="scope">
    /// Restricts results to shared entries plus the user's own; with a <see langword="null"/>
    /// <see cref="KnowledgeScopeFilter.UserId"/>, to shared entries only. <see langword="null"/> applies no filter.
    /// </param>
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(string collectionName, float[] queryEmbedding, int topK = 5, float scoreThreshold = 0.0f, KnowledgeScopeFilter? scope = null, CancellationToken ct = default);

    /// <summary>
    /// Deletes all entries whose <c>source</c> exactly matches <paramref name="source"/>.
    /// </summary>
    Task DeleteBySourceAsync(string collectionName, string source, CancellationToken ct = default);

    /// <returns><see langword="null"/> when the collection holds no entries.</returns>
    Task<CollectionStats?> GetCollectionStatsAsync(string collectionName, CancellationToken ct = default);

    Task DeleteCollectionAsync(string collectionName, CancellationToken ct = default);

    /// <summary>
    /// Oldest first.
    /// </summary>
    /// <param name="scope">As for <see cref="SearchAsync"/>.</param>
    Task<IReadOnlyList<DocumentChunk>> GetPageAsync(string collectionName, ulong limit = 200, ulong? offset = null, KnowledgeScopeFilter? scope = null, CancellationToken ct = default);
}
