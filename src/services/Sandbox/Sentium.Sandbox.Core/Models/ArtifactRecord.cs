namespace Sentium.Sandbox.Core.Models;

public sealed record ArtifactRecord
{
    public required string FileName { get; init; }
    public required string MimeType { get; init; }
    public required Uri BlobUri { get; init; }
    public required long SizeBytes { get; init; }
}
