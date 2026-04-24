using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record SearchFoodsQuery : IRequest<PagedResult<FoodDto>>, IPagedQuery, ISortableQuery
{
    public string? SearchTerm { get; init; }
    public string? Barcode { get; init; }
    public decimal? MinProteinCalorieRatio { get; init; }
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
    public decimal ProteinCalorieRatio { get; init; }
    public bool IsVerified { get; init; }
}

public class SearchFoodsQueryHandler : IRequestHandler<SearchFoodsQuery, PagedResult<FoodDto>>
{
    private static readonly Dictionary<string, Expression<Func<Domain.Entities.Food, object>>> SortMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = f => f.Name,
        ["calories"] = f => f.CaloriesPer100g,
        ["protein"] = f => f.ProteinPer100g,
        ["verified"] = f => f.IsVerified,
        ["proteinCalorieRatio"] = f => f.ProteinCalorieRatio
    };

    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    private readonly IMizanDbContext _context;
    private readonly HybridCache _cache;

    public SearchFoodsQueryHandler(IMizanDbContext context, HybridCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<PagedResult<FoodDto>> Handle(SearchFoodsQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"foods:search:{request.SearchTerm?.ToLower() ?? ""}:{request.Barcode ?? ""}:{request.MinProteinCalorieRatio}:{request.Page}:{request.PageSize}:{request.SortBy ?? ""}:{request.SortOrder ?? ""}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            request,
            LoadAsync,
            CacheOptions,
            tags: new[] { CacheTags.Foods },
            cancellationToken: cancellationToken);
    }

    private async ValueTask<PagedResult<FoodDto>> LoadAsync(SearchFoodsQuery request, CancellationToken cancellationToken)
    {
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

        if (request.MinProteinCalorieRatio.HasValue)
        {
            query = query.Where(f => f.ProteinCalorieRatio >= request.MinProteinCalorieRatio.Value);
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
                ProteinCalorieRatio = f.ProteinCalorieRatio,
                IsVerified = f.IsVerified
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<FoodDto>
        {
            Items = foods,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
