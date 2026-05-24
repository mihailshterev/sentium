namespace Sentium.Sandbox.Api.Dtos;

public sealed record SandboxExecutionResponse
{
    public required bool Succeeded { get; init; }
    public required long ExitCode { get; init; }
    public required string Output { get; init; }
    public required string Error { get; init; }
    public required bool TimedOut { get; init; }
    public required Guid JobId { get; init; }
    public required Guid SentinelAuditId { get; init; }
    public required long DurationMs { get; init; }
    public IReadOnlyList<ArtifactDto> Artifacts { get; init; } = [];
}

public sealed record ArtifactDto
{
    public required string FileName { get; init; }
    public required string MimeType { get; init; }
    public required string BlobUri { get; init; }
    public required string DownloadPath { get; init; }
    public required long SizeBytes { get; init; }
}
