namespace Sentium.Sandbox.Core.Models;

public sealed record ExecutionLogEntry
{
    public required Guid JobId { get; init; }
    public required DateTimeOffset ExecutedAt { get; init; }
    public required string AgentId { get; init; }
    public required string CorrelationId { get; init; }
    public required ExecutionLanguage Language { get; init; }
    public required string Code { get; init; }
    public string? OriginalUserPrompt { get; init; }
    public List<SandboxFileContext> FileContext { get; init; } = [];
    public required bool Succeeded { get; init; }
    public required long ExitCode { get; init; }
    public required string Output { get; init; }
    public required string Error { get; init; }
    public required bool TimedOut { get; init; }
    public required bool PolicyDenied { get; init; }
    public string? PolicyDenialReason { get; init; }
    public required Guid SentinelAuditId { get; init; }
    public required long DurationMs { get; init; }
    public List<ArtifactRecord> Artifacts { get; init; } = [];
}
