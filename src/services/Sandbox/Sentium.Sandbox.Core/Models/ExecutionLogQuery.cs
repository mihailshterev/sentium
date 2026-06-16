using Sentium.Shared.Results;

namespace Sentium.Sandbox.Core.Models;

public sealed record ExecutionLogQuery : PaginationQuery
{
    public ExecutionStatusFilter? Status { get; init; }
    public ExecutionLanguage? Language { get; init; }
    public string? Search { get; init; }
}
