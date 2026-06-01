namespace Sentium.Sandbox.Api.Dtos;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
