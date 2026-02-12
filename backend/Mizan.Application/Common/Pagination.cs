namespace Mizan.Application.Common;

public interface IPagedQuery
{
    int Page { get; init; }
    int PageSize { get; init; }
}

public interface ISortableQuery
{
    string? SortBy { get; init; }
    string? SortOrder { get; init; }
}

public record PagedResult<T>
{
    public List<T> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
