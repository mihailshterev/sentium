namespace Sentium.Sandbox.Api.Dtos;

public sealed record SandboxExecutionRequest
{
    public required string Language { get; init; }
    public required string Code { get; init; }
    public IReadOnlyList<SandboxFileContextDto>? FileContext { get; init; }
    public required string AgentId { get; init; }
    public string? OriginalUserPrompt { get; init; }
}

public sealed record SandboxFileContextDto
{
    public required string FileName { get; init; }
    public required string Content { get; init; }
}
