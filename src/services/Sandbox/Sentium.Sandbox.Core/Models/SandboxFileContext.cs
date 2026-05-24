namespace Sentium.Sandbox.Core.Models;

public sealed record SandboxFileContext
{
    public required string FileName { get; init; }
    public required string Content { get; init; }
}
