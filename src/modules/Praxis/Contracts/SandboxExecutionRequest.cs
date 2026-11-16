namespace Sentium.Praxis.Contracts;

public sealed record SandboxExecutionRequest
{
    public required string Language { get; init; }
    public required string Code { get; init; }
    public IReadOnlyList<SandboxFileContextDto>? FileContext { get; init; }
    public required string AgentId { get; init; }
    public string? OriginalUserPrompt { get; init; }

    /// <summary>
    /// The user the run acts for. It owns the execution history entry and is reported to Sentinel; a run without one
    /// is visible to Sovereigns only.
    /// </summary>
    public Guid? UserId { get; init; }
}
