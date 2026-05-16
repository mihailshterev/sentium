namespace Sentium.Sandbox.Core.Models;

public sealed record JobContext
{
    public required Guid JobId { get; init; }
    public required string JobDirectory { get; init; }
    public required ExecutionLanguage Language { get; init; }
}
