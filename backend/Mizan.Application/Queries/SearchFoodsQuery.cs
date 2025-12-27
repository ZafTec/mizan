using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record SearchFoodsQuery : IRequest<SearchFoodsResult>
{
    public string SearchTerm { get; init; } = string.Empty;
    public string? Barcode { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public record SearchFoodsResult
{
    public List<FoodDto> Foods { get; init; } = new();
    public int TotalCount { get; init; }
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

public class SearchFoodsQueryHandler : IRequestHandler<SearchFoodsQuery, SearchFoodsResult>
{
    private readonly IMizanDbContext _context;
    private readonly IRedisCacheService _cache;

    public SearchFoodsQueryHandler(IMizanDbContext context, IRedisCacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<SearchFoodsResult> Handle(SearchFoodsQuery request, CancellationToken cancellationToken)
    {
        // Generate cache key based on query parameters
        var cacheKey = $"foods:search:{request.SearchTerm?.ToLower() ?? ""}:{request.Barcode ?? ""}:{request.Page}:{request.PageSize}";

        // Try to get from cache first
        var cachedResult = await _cache.GetAsync<SearchFoodsResult>(cacheKey, cancellationToken);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        // Query database
        IQueryable<Domain.Entities.Food> query = _context.Foods;

        // Search by barcode if provided
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

        var foods = await query
            .OrderByDescending(f => f.IsVerified)
            .ThenBy(f => f.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
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

        var result = new SearchFoodsResult { Foods = foods, TotalCount = totalCount };

        // Cache result for 1 hour
        await _cache.SetAsync(cacheKey, result, TimeSpan.FromHours(1), cancellationToken);

        return result;
    }
}
