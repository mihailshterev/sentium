namespace Sentium.AgentRuntime.Core.Rag.Models;

/// <summary>
/// An atomic piece of text that has been chunked, embedded, and stored in the vector database.
/// Each chunk carries enough metadata for the agent to cite its source.
/// </summary>
public sealed class DocumentChunk
{
    /// <summary>
    /// Stable identifier used as the Qdrant point ID.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// The text content of this chunk.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Human-readable label of the originating service or file.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Enum-typed classification of the data origin.
    /// </summary>
    public IngestionSourceType SourceType { get; init; }

    /// <summary>
    /// Arbitrary key-value metadata stored alongside the vector (e.g. filename, device-id).
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Wall-clock time at which this chunk was ingested.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Visibility scope of this chunk (<see cref="KnowledgeScope.Shared"/> or <see cref="KnowledgeScope.User"/>).
    /// </summary>
    public string Scope { get; init; } = KnowledgeScope.Shared;

    /// <summary>
    /// The owning user when <see cref="Scope"/> is <see cref="KnowledgeScope.User"/>; otherwise <c>null</c>.
    /// </summary>
    public Guid? UserId { get; init; }
}
