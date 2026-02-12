using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record SearchFoodsQuery : IRequest<PagedResult<FoodDto>>, IPagedQuery, ISortableQuery
{
    public string? SearchTerm { get; init; }
    public string? Barcode { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}

public record FoodDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Brand { get; init; }
    public string? Barcode { get; init; }
    public decimal ServingSize { get; init; }
    public string ServingUnit { get; init; } = string.Empty;
    public decimal CaloriesPer100g { get; init; }
    public decimal ProteinPer100g { get; init; }
    public decimal CarbsPer100g { get; init; }
    public decimal FatPer100g { get; init; }
    public decimal? FiberPer100g { get; init; }
    public bool IsVerified { get; init; }
}

public class SearchFoodsQueryHandler : IRequestHandler<SearchFoodsQuery, PagedResult<FoodDto>>
{
    private static readonly Dictionary<string, Expression<Func<Domain.Entities.Food, object>>> SortMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = f => f.Name,
        ["calories"] = f => f.CaloriesPer100g,
        ["protein"] = f => f.ProteinPer100g,
        ["verified"] = f => f.IsVerified
    };

    private readonly IMizanDbContext _context;
    private readonly IRedisCacheService _cache;

    public SearchFoodsQueryHandler(IMizanDbContext context, IRedisCacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<PagedResult<FoodDto>> Handle(SearchFoodsQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"foods:search:{request.SearchTerm?.ToLower() ?? ""}:{request.Barcode ?? ""}:{request.Page}:{request.PageSize}:{request.SortBy ?? ""}:{request.SortOrder ?? ""}";

        var cachedResult = await _cache.GetAsync<PagedResult<FoodDto>>(cacheKey, cancellationToken);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        IQueryable<Domain.Entities.Food> query = _context.Foods;

        if (!string.IsNullOrWhiteSpace(request.Barcode))
        {
            query = query.Where(f => f.Barcode == request.Barcode);
        }
        else if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.ToLower();
            query = query.Where(f =>
                f.Name.ToLower().Contains(searchTerm) ||
                (f.Brand != null && f.Brand.ToLower().Contains(searchTerm)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var sortedQuery = query.ApplySorting(
            request,
            SortMappings,
            defaultSort: f => f.Name,
            defaultDescending: false);

        var foods = await sortedQuery
            .ApplyPaging(request)
            .Select(f => new FoodDto
            {
                Id = f.Id,
                Name = f.Name,
                Brand = f.Brand,
                Barcode = f.Barcode,
                ServingSize = f.ServingSize,
                ServingUnit = f.ServingUnit,
                CaloriesPer100g = f.CaloriesPer100g,
                ProteinPer100g = f.ProteinPer100g,
                CarbsPer100g = f.CarbsPer100g,
                FatPer100g = f.FatPer100g,
                FiberPer100g = f.FiberPer100g,
                IsVerified = f.IsVerified
            })
            .ToListAsync(cancellationToken);

        var result = new PagedResult<FoodDto>
        {
            Items = foods,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromHours(1), cancellationToken);

        return result;
    }
}
