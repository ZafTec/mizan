using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Nutrition;
using Mizan.Domain.Nutrition;
using Mizan.Domain.Streaks;

namespace Mizan.Application.Queries;

/// <summary>
/// The person's saved recipes (their own, their household's and the public ones they favourited,
/// as <see cref="GetRecipesQuery"/> lists them) ranked by how well one serving fits what is left
/// of a day's targets (<see cref="RecipeFit"/>). The day defaults to today in the person's zone.
/// </summary>
public record RankRecipesForDayQuery : IRequest<RecipeFitResult>
{
    public DateOnly? Date { get; init; }

    /// <summary>How many recipes to return, best first, 1 to 20.</summary>
    public int Count { get; init; } = 5;
}

public class RankRecipesForDayQueryHandler : IRequestHandler<RankRecipesForDayQuery, RecipeFitResult>
{
    private const int MaxRecipes = 200;
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IMediator _mediator;

    public RankRecipesForDayQueryHandler(IMizanDbContext context, ICurrentUserService currentUser, IMediator mediator)
    {
        _context = context;
        _currentUser = currentUser;
        _mediator = mediator;
    }

    public async Task<RecipeFitResult> Handle(RankRecipesForDayQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var date = request.Date ?? StreakClock.Today(
            await _context.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.TimeZoneId).FirstOrDefaultAsync(cancellationToken),
            DateTimeOffset.UtcNow);

        // The same totals, targets and recipe list the person sees, under the same access rules.
        var day = await _mediator.Send(new GetDailyNutritionQuery { Date = date }, cancellationToken);
        var recipes = await _mediator.Send(new GetRecipesQuery
        {
            IncludePublic = false,
            IncludeFavorites = true,
            PageSize = MaxRecipes,
        }, cancellationToken);

        var targets = new DayTargets(day.TargetCalories, day.TargetProtein, day.TargetCarbs, day.TargetFat);
        var eaten = new Macros(day.TotalCalories, day.TotalProtein, day.TotalCarbs, day.TotalFat);
        var known = recipes.Items.Where(r => r.Nutrition?.CaloriesPerServing is not null).ToList();
        var ranked = RecipeFit.Rank(eaten, targets, known, r => new Macros(
            r.Nutrition!.CaloriesPerServing ?? 0m, r.Nutrition.ProteinGrams ?? 0m, r.Nutrition.CarbsGrams ?? 0m, r.Nutrition.FatGrams ?? 0m));

        decimal? Left(decimal? target, decimal value) => target is > 0 ? Math.Round(value, 1) : null;
        var left = RecipeFit.Remaining(eaten, targets);

        return new RecipeFitResult
        {
            Date = date,
            CaloriesLeft = Left(targets.Calories, left.Calories),
            ProteinLeft = Left(targets.Protein, left.Protein),
            CarbsLeft = Left(targets.Carbs, left.Carbs),
            FatLeft = Left(targets.Fat, left.Fat),
            Note = ranked.Count > 0 ? null
                : known.Count == 0 ? "No saved recipe has nutrition to rank."
                : "No daily target is set, so nothing can be ranked against what is left.",
            Recipes = ranked.Take(Math.Clamp(request.Count, 1, 20)).Select((r, i) => new RecipeFitItem
            {
                Rank = i + 1,
                RecipeId = r.Item.Id,
                Title = r.Item.Title,
                Score = r.Score,
                Calories = r.Item.Nutrition!.CaloriesPerServing ?? 0m,
                Protein = r.Item.Nutrition.ProteinGrams ?? 0m,
                Carbs = r.Item.Nutrition.CarbsGrams ?? 0m,
                Fat = r.Item.Nutrition.FatGrams ?? 0m,
                CaloriesLeftAfter = Left(targets.Calories, r.LeftAfter.Calories),
                ProteinLeftAfter = Left(targets.Protein, r.LeftAfter.Protein),
                CarbsLeftAfter = Left(targets.Carbs, r.LeftAfter.Carbs),
                FatLeftAfter = Left(targets.Fat, r.LeftAfter.Fat),
            }).ToList(),
        };
    }
}
