namespace Sentium.Sandbox.Core.Models;

public sealed record ExecutionResult
{
    public required bool Succeeded { get; init; }
    public required long ExitCode { get; init; }
    public required string Output { get; init; }
    public required string Error { get; init; }
    public required bool TimedOut { get; init; }
    public required bool PolicyDenied { get; init; }
    public string? PolicyDenialReason { get; init; }
    public Guid SentinelAuditId { get; init; }
    public string? ContainerId { get; init; }
    public required Guid JobId { get; init; }
    public required long DurationMs { get; init; }
    public IReadOnlyList<ArtifactRecord> Artifacts { get; init; } = [];
}
