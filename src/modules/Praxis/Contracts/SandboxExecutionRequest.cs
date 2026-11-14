namespace Sentium.Praxis.Contracts;

public sealed record SandboxExecutionRequest
{
    public required string Language { get; init; }
    public required string Code { get; init; }
    public IReadOnlyList<SandboxFileContextDto>? FileContext { get; init; }
    public required string AgentId { get; init; }
    public string? OriginalUserPrompt { get; init; }
}
