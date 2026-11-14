namespace Sentium.Praxis.Contracts;

public sealed record SandboxFileContextDto
{
    public required string FileName { get; init; }
    public required string Content { get; init; }
}
