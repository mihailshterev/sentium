using Sentium.Codex.Contracts.Models;

namespace Sentium.Codex.Contracts;

/// <summary>
/// Strongly-typed configuration for the RAG pipeline.
/// Bind from appsettings section "Rag".
/// </summary>
public sealed class RagOptions
{
    public const string SectionName = "Rag";

    /// <summary>
    /// Collection that holds ingested knowledge-base documents (the default ingestion/search target).
    /// </summary>
    public string CollectionName { get; set; } = KnowledgeCollections.KnowledgeBase;

    /// <summary>
    /// Collections a unified <c>knowledge_base_search</c> spans, so retrieval is not coupled to which store a
    /// fact was written to. Defaults to the knowledge base, captured learnings, and saved memories.
    /// </summary>
    public string[] SearchCollections { get; set; } = KnowledgeCollections.All;

    /// <summary>
    /// Embedding model requested from the embedding server. Its output size must match the vector store's
    /// <c>vector(768)</c> column (<c>CodexDbContext.EmbeddingDimensions</c>).
    /// </summary>
    public string EmbeddingModelName { get; set; } = "embeddinggemma-2:740m";

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
}
