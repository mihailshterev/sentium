using Pgvector;
using Sentium.Codex.Contracts.Models;

namespace Sentium.Codex.Entities;

/// <summary>
/// A stored, embedded piece of knowledge. One row per <see cref="DocumentChunk"/>; the
/// <see cref="Collection"/> column partitions the rows into the logical vector collections
/// (<see cref="KnowledgeCollections"/>).
/// </summary>
internal sealed class KnowledgeChunk
{
    public Guid Id { get; set; }

    public required string Collection { get; set; }

    public required string Content { get; set; }

    public required string Source { get; set; }

    public IngestionSourceType SourceType { get; set; }

    public string Scope { get; set; } = KnowledgeScope.Shared;

    public Guid? UserId { get; set; }

    public Dictionary<string, string> Metadata { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public required Vector Embedding { get; set; }

    /// <summary>
    /// The embedding model that produced <see cref="Embedding"/> (<c>Rag:EmbeddingModelName</c> at write time).
    /// Rows from another model are re-embedded by <c>EmbeddingReindexService</c>, so a search never compares
    /// vectors from different models.
    /// </summary>
    public string EmbeddingModel { get; set; } = string.Empty;
}
