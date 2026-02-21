using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetFoodDiaryQuery : IRequest<FoodDiaryResult>
{
    public DateOnly Date { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);
}

public record FoodDiaryResult
{
    public DateOnly Date { get; init; }
    public List<FoodDiaryEntryDto> Entries { get; init; } = new();
    public DailyTotalsDto Totals { get; init; } = new();
}

public record FoodDiaryEntryDto
{
    public Guid Id { get; init; }
    public string MealType { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal Servings { get; init; }
    public decimal? Calories { get; init; }
    public decimal? ProteinGrams { get; init; }
    public decimal? CarbsGrams { get; init; }
    public decimal? FatGrams { get; init; }
    public decimal? FiberGrams { get; init; }
    public DateTime LoggedAt { get; init; }
}

public record DailyTotalsDto
{
    public decimal Calories { get; init; }
    public decimal Protein { get; init; }
    public decimal Carbs { get; init; }
    public decimal Fat { get; init; }
    public decimal Fiber { get; init; }
}

public class GetFoodDiaryQueryHandler : IRequestHandler<GetFoodDiaryQuery, FoodDiaryResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetFoodDiaryQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<FoodDiaryResult> Handle(GetFoodDiaryQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return new FoodDiaryResult { Date = request.Date };
        }

        var entries = await _context.FoodDiaryEntries
            .Include(e => e.Food)
            .Include(e => e.Recipe)
            .Where(e => e.UserId == _currentUser.UserId && e.EntryDate == request.Date)
            .OrderBy(e => e.LoggedAt)
            .Select(e => new FoodDiaryEntryDto
            {
                Id = e.Id,
                MealType = e.MealType,
                Name = !string.IsNullOrEmpty(e.Name) ? e.Name : (e.Food != null ? e.Food.Name : (e.Recipe != null ? e.Recipe.Title : "Unknown")),
                Servings = e.Servings,
                Calories = e.Calories,
                ProteinGrams = e.ProteinGrams,
                CarbsGrams = e.CarbsGrams,
                FatGrams = e.FatGrams,
                FiberGrams = e.FiberGrams,
                LoggedAt = e.LoggedAt
            })
            .ToListAsync(cancellationToken);

        var totals = new DailyTotalsDto
        {
            Calories = entries.Sum(e => e.Calories ?? 0),
            Protein = entries.Sum(e => e.ProteinGrams ?? 0),
            Carbs = entries.Sum(e => e.CarbsGrams ?? 0),
            Fat = entries.Sum(e => e.FatGrams ?? 0),
            Fiber = entries.Sum(e => e.FiberGrams ?? 0)
        };

        return new FoodDiaryResult
        {
            Date = request.Date,
            Entries = entries,
            Totals = totals
        };
    }
}
