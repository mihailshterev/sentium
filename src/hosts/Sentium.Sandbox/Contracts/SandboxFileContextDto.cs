namespace Sentium.Sandbox.Contracts;

internal sealed record SandboxFileContextDto
{
    public required string FileName { get; init; }
    public required string Content { get; init; }
}
