using Sentium.Core.Common;
using Sentium.Praxis.Features.Files;

namespace Sentium.Praxis.Entities;

/// <summary>
/// The content lives in object storage under <see cref="BlobName"/>; the row tracks its ingestion into the knowledge
/// base.
/// </summary>
internal sealed class ProjectFile : IUserOwned
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// Can be null if the file is not associated with a specific workspace
    /// (e.g., when used for global agent context).
    /// </summary>
    public Guid? WorkspaceId { get; set; }

    public string FileName { get; set; } = null!;

    public Guid BlobName { get; set; }

    /// <summary>
    /// Includes the dot (".txt"). Validated against <see cref="AllowedFileTypes.Extensions"/> at upload time.
    /// </summary>
    public string Extension { get; set; } = null!;

    public long SizeBytes { get; set; }

    public FileProcessingStatus ProcessingStatus { get; set; }

    /// <summary>
    /// Why the last ingestion attempt failed; cleared when the file is queued again.
    /// </summary>
    public string? ProcessingError { get; set; }

    public DateTime CreatedAt { get; set; }

    public Workspace? Workspace { get; set; }
}

