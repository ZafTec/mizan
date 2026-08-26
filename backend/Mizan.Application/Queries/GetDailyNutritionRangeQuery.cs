using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetDailyNutritionRangeQuery : IRequest<DailyNutritionRangeResult>
{
    public int Days { get; init; } = 7;
    public DateOnly? EndDate { get; init; }
}

public record DailyNutritionRangeResult
{
    public List<DailyNutritionSummaryDto> Days { get; init; } = new();
}

public record DailyNutritionSummaryDto
{
    public DateOnly Date { get; init; }
    public decimal Calories { get; init; }
    public decimal Protein { get; init; }
    public decimal Carbs { get; init; }
    public decimal Fat { get; init; }
    public decimal Fiber { get; init; }
}

public class GetDailyNutritionRangeQueryHandler : IRequestHandler<GetDailyNutritionRangeQuery, DailyNutritionRangeResult>
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(24),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly HybridCache _cache;

    public GetDailyNutritionRangeQueryHandler(IMizanDbContext context, ICurrentUserService currentUser, HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<DailyNutritionRangeResult> Handle(GetDailyNutritionRangeQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return new DailyNutritionRangeResult();
        }

        var userId = _currentUser.UserId.Value;
        var endDate = request.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        return await _cache.GetOrCreateAsync(
            $"nutrition:range:{userId}:{request.Days}:{endDate:yyyy-MM-dd}",
            (UserId: userId, request.Days, EndDate: endDate),
            LoadAsync,
            CacheOptions,
            tags: [CacheTags.Nutrition(userId)],
            cancellationToken: cancellationToken);
    }

    private async ValueTask<DailyNutritionRangeResult> LoadAsync(
        (Guid UserId, int Days, DateOnly EndDate) state, CancellationToken cancellationToken)
    {
        var startDate = state.EndDate.AddDays(-(state.Days - 1));

        var days = await _context.FoodDiaryEntries
            .Where(e => e.UserId == state.UserId && e.EntryDate >= startDate && e.EntryDate <= state.EndDate)
            .GroupBy(e => e.EntryDate)
            .Select(g => new DailyNutritionSummaryDto
            {
                Date = g.Key,
                Calories = g.Sum(e => e.Calories ?? 0),
                Protein = g.Sum(e => e.ProteinGrams ?? 0),
                Carbs = g.Sum(e => e.CarbsGrams ?? 0),
                Fat = g.Sum(e => e.FatGrams ?? 0),
                Fiber = g.Sum(e => e.FiberGrams ?? 0)
            })
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken);

        return new DailyNutritionRangeResult { Days = days };
    }
}
