namespace Sentium.Sandbox.Models;

internal sealed record SandboxFileContext
{
    public required string FileName { get; init; }
    public required string Content { get; init; }
}
