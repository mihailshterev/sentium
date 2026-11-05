using Pgvector;
using Sentium.Codex.Contracts.Models;

namespace Sentium.Codex.Entities;

/// <summary>
/// A stored, embedded piece of knowledge. One row per <see cref="DocumentChunk"/>; the
/// <see cref="Collection"/> column partitions the rows into the logical vector collections
/// (<see cref="KnowledgeCollections"/>).
/// </summary>
public sealed class KnowledgeChunk
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
}
