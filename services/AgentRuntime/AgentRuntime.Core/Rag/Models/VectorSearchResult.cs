namespace AgentRuntime.Core.Rag.Models;

/// <summary>
/// A single result returned from a vector similarity search,
/// pairing the matching chunk with its relevance score.
/// </summary>
public sealed class VectorSearchResult
{
    /// <summary>
    /// The matching document chunk.
    /// </summary>
    public required DocumentChunk Chunk { get; init; }

    /// <summary>
    /// Cosine similarity score in the range [0, 1].
    /// Higher values indicate greater relevance.
    /// </summary>
    public float Score { get; init; }
}
