namespace Sentium.Shared.Results;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages)
{
    public static PagedResponse<T> Create(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        var totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResponse<T>(items, totalCount, page, pageSize, totalPages);
    }
}
