namespace Sentium.Praxis.Contracts;

public sealed record SandboxExecutionResponse
{
    public required bool Succeeded { get; init; }
    public required long ExitCode { get; init; }
    public required string Output { get; init; }
    public required string Error { get; init; }
    public required bool TimedOut { get; init; }
    public bool PolicyDenied { get; init; }
    public string? PolicyDenialReason { get; init; }
    public required Guid JobId { get; init; }
    public required Guid SentinelAuditId { get; init; }
    public required long DurationMs { get; init; }
    public IReadOnlyList<ArtifactDto> Artifacts { get; init; } = [];
}
