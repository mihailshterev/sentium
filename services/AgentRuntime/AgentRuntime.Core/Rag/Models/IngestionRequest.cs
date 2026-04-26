namespace AgentRuntime.Core.Rag.Models;

/// <summary>
/// Carries a single document payload submitted for ingestion.
/// Source services populate this record and hand it to <see cref="IDocumentIngestionService"/>.
/// </summary>
public sealed class IngestionRequest
{
    /// <summary>
    /// The raw text to be chunked, embedded, and stored.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Human-readable label identifying the originating service or file.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Enum-typed classification used for filtering and citation.
    /// </summary>
    public IngestionSourceType SourceType { get; init; }

    /// <summary>
    /// Optional key-value metadata forwarded to each resulting <see cref="DocumentChunk"/>.
    /// Useful for device IDs, log file paths, timestamps, etc.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}
