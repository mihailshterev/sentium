namespace Sentium.Sandbox.Models;

internal sealed record ArtifactRecord
{
    public required string FileName { get; init; }
    public required string MimeType { get; init; }
    public required Uri BlobUri { get; init; }

    /// <summary>
    /// The object key in the artifact bucket, <c>{jobId:N}/{FileName}</c>.
    /// </summary>
    public required string Key { get; init; }

    public required long SizeBytes { get; init; }
}
