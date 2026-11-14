using Sentium.Core.Common;
using Sentium.Praxis.Features.Files;

namespace Sentium.Praxis.Entities;

/// <summary>
/// Project files are stored in blob storage while metadata is maintained in the database.
/// Files go through an ingestion pipeline (<see cref="FileProcessingStatus"/>) for RAG vectorization
/// and semantic search integration.
/// </summary>
public sealed class ProjectFile : IUserOwned
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// Can be null if the file is not associated with a specific workspace
    /// (e.g., when used for global agent context).
    /// </summary>
    public Guid? WorkspaceId { get; set; }

    /// <summary>
    /// The user-provided filename; it may differ from how the file is stored in blob storage.
    /// </summary>
    public string FileName { get; set; } = null!;

    public Guid BlobName { get; set; }

    /// <summary>
    /// Includes the dot (e.g., ".txt", ".json"). Validated against <see cref="AllowedFileTypes.Extensions"/> at upload time.
    /// </summary>
    public string Extension { get; set; } = null!;

    public long SizeBytes { get; set; }

    /// <summary>
    /// Files move through states: Pending → Processing → Completed (or Failed).
    /// </summary>
    public FileProcessingStatus ProcessingStatus { get; set; }

    public DateTime CreatedAt { get; set; }

    public Workspace? Workspace { get; set; }
}

