using MediatR;
using Microsoft.EntityFrameworkCore;
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
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetDailyNutritionRangeQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<DailyNutritionRangeResult> Handle(GetDailyNutritionRangeQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return new DailyNutritionRangeResult();
        }

        var endDate = request.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-(request.Days - 1));

        var days = await _context.FoodDiaryEntries
            .Where(e => e.UserId == _currentUser.UserId && e.EntryDate >= startDate && e.EntryDate <= endDate)
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
