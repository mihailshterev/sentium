namespace Sentium.AgentRuntime.Core.Storage;

/// <summary>
/// Defines event type constants for file-related domain events.
/// </summary>
public static class FileEvents
{
    /// <summary>
    /// Event type published when a file has been ingested into the RAG vectorization pipeline.
    /// </summary>
    /// <remarks>
    /// The background worker (<see cref="Infrastructure.Storage.FileIngestionWorker"/>) subscribes to this event
    /// and processes files for RAG indexing.
    /// </remarks>
    public const string FileIngested = "internal.file.ingested";
}

/// <summary>
/// Represents a domain event indicating that a file has been uploaded and should be ingested.
/// </summary>
/// <remarks>
/// <para>
/// This event is published after a file is successfully stored in blob storage and its metadata
/// is recorded in the database. The background ingestion worker processes this event to:
/// 1. Read the file content from blob storage.
/// 2. Vectorize the content using a semantic model.
/// 3. Store the vectors in a RAG index (e.g., Qdrant).
/// 4. Update the file's <see cref="Core.Files.FileProcessingStatus"/> to Completed or Failed.
/// </para>
/// </remarks>
public record FileIngestedEvent(Guid FileId, Guid? WorkspaceId);
