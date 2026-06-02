namespace Sentium.Sandbox.Core.Models;

public sealed record ExecutionLogQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public ExecutionStatusFilter? Status { get; init; }
    public ExecutionLanguage? Language { get; init; }
    public string? Search { get; init; }
}
