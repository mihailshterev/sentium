namespace Sentium.Sandbox.Core.Models;

public sealed record ExecutionRequest
{
    public required ExecutionLanguage Language { get; init; }
    public required string Code { get; init; }
    public IReadOnlyList<SandboxFileContext> FileContext { get; init; } = [];
    public required string AgentId { get; init; }
    public required string CorrelationId { get; init; }
    public string? OriginalUserPrompt { get; init; }
}
