using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Mizan.Application.Common;

public static class QueryableExtensions
{
    public static IQueryable<T> ApplyPaging<T>(this IQueryable<T> query, IPagedQuery paging)
    {
        return query
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize);
    }

    public static IQueryable<T> ApplySorting<T>(
        this IQueryable<T> query,
        ISortableQuery sorting,
        Dictionary<string, Expression<Func<T, object>>> sortMappings,
        Expression<Func<T, object>> defaultSort,
        bool defaultDescending = false)
    {
        var isDescending = string.Equals(sorting.SortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(sorting.SortBy) &&
            sortMappings.TryGetValue(sorting.SortBy.ToLowerInvariant(), out var sortExpression))
        {
            return isDescending
                ? query.OrderByDescending(sortExpression)
                : query.OrderBy(sortExpression);
        }

        return defaultDescending
            ? query.OrderByDescending(defaultSort)
            : query.OrderBy(defaultSort);
    }

    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        IPagedQuery paging,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .ApplyPaging(paging)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            Page = paging.Page,
            PageSize = paging.PageSize
        };
    }
}
