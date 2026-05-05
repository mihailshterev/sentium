using Sentium.AgentRuntime.Core.Files;

namespace Sentium.AgentRuntime.Core.Entities;

public sealed class ProjectFile
{
    public Guid Id { get; set; }
    public Guid? WorkspaceId { get; set; }
    public string FileName { get; set; } = null!;
    public Guid BlobName { get; set; }
    public string Extension { get; set; } = null!;
    public long SizeBytes { get; set; }
    public FileProcessingStatus ProcessingStatus { get; set; }
    public DateTime CreatedAt { get; set; }

    public Workspace? Workspace { get; set; }
}

