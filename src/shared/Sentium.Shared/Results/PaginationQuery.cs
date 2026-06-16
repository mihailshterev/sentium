namespace Sentium.Shared.Results;

public record PaginationQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxListCap = 500;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;
    public (int Page, int PageSize) Normalize() => (Math.Max(1, Page), Math.Clamp(PageSize, 1, MaxPageSize));
}
