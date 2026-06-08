namespace Sentium.Sandbox.Core.Dtos;

public sealed record SandboxExecutionLogRecord
{
    public required Guid JobId { get; init; }
    public required DateTimeOffset ExecutedAt { get; init; }
    public required string AgentId { get; init; }
    public required string CorrelationId { get; init; }
    public required string Language { get; init; }
    public required string Code { get; init; }
    public string? OriginalUserPrompt { get; init; }
    public IReadOnlyList<SandboxFileContextDto> FileContext { get; init; } = [];
    public required bool Succeeded { get; init; }
    public required long ExitCode { get; init; }
    public required string Output { get; init; }
    public required string Error { get; init; }
    public required bool TimedOut { get; init; }
    public required bool PolicyDenied { get; init; }
    public string? PolicyDenialReason { get; init; }
    public required Guid SentinelAuditId { get; init; }
    public required long DurationMs { get; init; }
    public IReadOnlyList<ArtifactDto> Artifacts { get; init; } = [];
}
