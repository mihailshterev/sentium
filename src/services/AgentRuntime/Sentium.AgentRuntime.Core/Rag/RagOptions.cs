namespace Sentium.AgentRuntime.Core.Rag;

/// <summary>
/// Strongly-typed configuration for the RAG pipeline.
/// Bind from appsettings section "Rag".
/// </summary>
public sealed class RagOptions
{
    public const string SectionName = "Rag";

    /// <summary>
    /// Qdrant collection that holds all embedded chunks.
    /// </summary>
    public string CollectionName { get; set; } = "knowledge_base";

    /// <summary>
    /// Ollama model used for embedding generation.
    /// Must be pulled in the Ollama container before ingestion.
    /// Recommended: "nomic-embed-text" (768-dim) or "mxbai-embed-large" (1024-dim).
    /// </summary>
    public string EmbeddingModelName { get; set; } = "nomic-embed-text";

    /// <summary>
    /// Maximum character count per chunk (before overlap is applied).
    /// </summary>
    public int ChunkSize { get; set; } = 500;

    /// <summary>
    /// Number of characters that overlap between consecutive chunks to preserve context.
    /// </summary>
    public int ChunkOverlap { get; set; } = 50;

    /// <summary>
    /// Default number of results returned by a knowledge-base search.
    /// </summary>
    public int DefaultTopK { get; set; } = 5;

    /// <summary>
    /// Minimum cosine similarity score for a result to be included.
    /// </summary>
    public float ScoreThreshold { get; set; } = 0.5f;

    /// <summary>
    /// Dimensionality of the embedding vectors produced by <see cref="EmbeddingModelName"/>.
    /// Must match the model output: nomic-embed-text → 768, mxbai-embed-large → 1024.
    /// </summary>
    public uint VectorSize { get; set; } = 768;
}
