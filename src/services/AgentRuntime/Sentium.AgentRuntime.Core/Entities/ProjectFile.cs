using Sentium.AgentRuntime.Core.Files;

namespace Sentium.AgentRuntime.Core.Entities;

/// <summary>
/// Represents a project file that belongs to a workspace and is managed by the system.
/// </summary>
/// <remarks>
/// Project files are stored in cloud blob storage while metadata is maintained in the database.
/// Files go through an ingestion pipeline (<see cref="FileProcessingStatus"/>) for RAG vectorization
/// and semantic search integration.
/// </remarks>
public sealed class ProjectFile
{
    /// <summary>
    /// Gets or sets the unique identifier for this project file.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the workspace this file belongs to.
    /// </summary>
    /// <remarks>
    /// Can be null if the file is not associated with a specific workspace
    /// (e.g., when used for global agent context).
    /// </remarks>
    public Guid? WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the original name of the uploaded file.
    /// </summary>
    /// <remarks>
    /// This is the user-provided filename and may differ from how the file is stored in blob storage.
    /// </remarks>
    public string FileName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the unique blob identifier in cloud storage.
    /// </summary>
    /// <remarks>
    /// This refers to the actual storage location in Azure Blob Storage.
    /// </remarks>
    public Guid BlobName { get; set; }

    /// <summary>
    /// Gets or sets the file extension (including the dot, e.g., ".txt", ".json").
    /// </summary>
    /// <remarks>
    /// Extensions are validated against <see cref="AllowedFileTypes.Extensions"/> at upload time.
    /// </remarks>
    public string Extension { get; set; } = null!;

    /// <summary>
    /// Gets or sets the size of the file in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the current processing status of the file.
    /// </summary>
    /// <remarks>
    /// Files move through states: Pending → Processing → Completed (or Failed).
    /// See <see cref="FileProcessingStatus"/> for possible values.
    /// </remarks>
    public FileProcessingStatus ProcessingStatus { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this file was added to the system.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the associated workspace (navigation property).
    /// </summary>
    /// <remarks>
    /// Can be null if <see cref="WorkspaceId"/> is null.
    /// </remarks>
    public Workspace? Workspace { get; set; }
}

