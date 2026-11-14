namespace Sentium.Praxis.Contracts;

public sealed record ArtifactDto
{
    public required string FileName { get; init; }
    public required string MimeType { get; init; }
    public required string BlobUri { get; init; }
    public required string DownloadPath { get; init; }
    public required long SizeBytes { get; init; }
}
