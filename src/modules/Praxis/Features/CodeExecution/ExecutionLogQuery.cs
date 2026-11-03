using Sentium.Core.Results;

namespace Sentium.Praxis.Features.CodeExecution;

public sealed record ExecutionLogQuery : PaginationQuery
{
    public ExecutionStatusFilter? Status { get; init; }
    public ExecutionLanguage? Language { get; init; }
    public string? Search { get; init; }
}
