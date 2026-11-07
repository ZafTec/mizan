using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Application.Prices;
using Mizan.Contracts.Prices;

namespace Mizan.Application.Queries;

/// <summary>
/// One day of food: the totals against the targets, and on request that day's diary entries and
/// the active goal. Each part comes from the query the web reads it with.
/// </summary>
public sealed record GetNutritionDayQuery(DateOnly Date, bool IncludeEntries = false, bool IncludeGoal = false) : IRequest<NutritionDayResult>;

/// <summary>The day's totals (<see cref="DailyNutritionResult"/>), with the diary and the goal when they were asked for.</summary>
public sealed record NutritionDayResult : DailyNutritionResult
{
    public NutritionDayResult(DailyNutritionResult day) : base(day)
    {
    }

    public FoodDiaryResult? Diary { get; init; }

    /// <summary>The active goal; null when there is none or it was not asked for (see <see cref="HasGoal"/>).</summary>
    public UserGoalDto? Goal { get; init; }

    public bool? HasGoal { get; init; }
}

public sealed class GetNutritionDayQueryHandler : IRequestHandler<GetNutritionDayQuery, NutritionDayResult>
{
    private readonly ISender _sender;

    public GetNutritionDayQueryHandler(ISender sender) => _sender = sender;

    public async Task<NutritionDayResult> Handle(GetNutritionDayQuery request, CancellationToken cancellationToken)
    {
        var day = await _sender.Send(new GetDailyNutritionQuery { Date = request.Date }, cancellationToken);
        var diary = request.IncludeEntries ? await _sender.Send(new GetFoodDiaryQuery { Date = request.Date }, cancellationToken) : null;
        var goal = request.IncludeGoal ? await _sender.Send(new GetUserGoalQuery(), cancellationToken) : null;
        return new NutritionDayResult(day)
        {
            Diary = diary,
            Goal = goal,
            HasGoal = request.IncludeGoal ? goal is not null : null,
        };
    }
}

/// <summary>The active goal, every goal set before it, and the check-ins of the last <see cref="Days"/> days.</summary>
public sealed record GetGoalOverviewQuery(int Days = 30) : IRequest<GoalOverviewResult>;

public sealed record GoalOverviewResult(UserGoalDto? Current, IReadOnlyList<UserGoalDto> History, GoalProgressHistoryDto Progress);

public sealed class GetGoalOverviewQueryHandler : IRequestHandler<GetGoalOverviewQuery, GoalOverviewResult>
{
    private readonly ISender _sender;

    public GetGoalOverviewQueryHandler(ISender sender) => _sender = sender;

    public async Task<GoalOverviewResult> Handle(GetGoalOverviewQuery request, CancellationToken cancellationToken)
    {
        var current = await _sender.Send(new GetUserGoalQuery(), cancellationToken);
        var history = await _sender.Send(new GetUserGoalHistoryQuery(), cancellationToken);
        var progress = await _sender.Send(new GetGoalProgressHistoryQuery { Days = Math.Clamp(request.Days, 1, 366) }, cancellationToken);
        return new GoalOverviewResult(current, history, progress);
    }
}

/// <summary>One food, with its price in the viewer's region when asked. Not found when the viewer cannot see it.</summary>
public sealed record GetFoodDetailQuery(Guid FoodId, bool IncludePrice = false) : IRequest<FoodDetailResult>;

public sealed record FoodDetailResult : FoodDto
{
    public FoodDetailResult(FoodDto food) : base(food)
    {
    }

    public FoodPriceQuoteDto? Price { get; init; }
}

public sealed class GetFoodDetailQueryHandler : IRequestHandler<GetFoodDetailQuery, FoodDetailResult>
{
    private readonly ISender _sender;

    public GetFoodDetailQueryHandler(ISender sender) => _sender = sender;

    public async Task<FoodDetailResult> Handle(GetFoodDetailQuery request, CancellationToken cancellationToken)
    {
        var food = await _sender.Send(new GetFoodByIdQuery(request.FoodId), cancellationToken)
            ?? throw new EntityNotFoundException("Food", request.FoodId);
        var price = request.IncludePrice ? await _sender.Send(new GetFoodPriceQuery(request.FoodId), cancellationToken) : null;
        return new FoodDetailResult(food) { Price = price };
    }
}

/// <summary>
/// What deleting a recipe would remove or touch, for the person to see before they confirm: its
/// name, its ingredients, the meal plans that use it (which stop the delete), their own diary rows
/// that logged it and the ingredient made from it. Only the owner (or an administrator) sees it.
/// </summary>
public sealed record GetRecipeDeletePreviewQuery(Guid RecipeId) : IRequest<RecipeDeletePreview>;

public sealed record RecipeDeletePreview(
    Guid Id,
    string Title,
    int Ingredients,
    int MealPlans,
    int DiaryEntries,
    string? PreparationFood);

public sealed class GetRecipeDeletePreviewQueryHandler : IRequestHandler<GetRecipeDeletePreviewQuery, RecipeDeletePreview>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetRecipeDeletePreviewQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<RecipeDeletePreview> Handle(GetRecipeDeletePreviewQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var recipe = await _context.Recipes.AsNoTracking()
            .Where(r => r.Id == request.RecipeId)
            .Select(r => new { r.Id, r.Title, r.UserId })
            .FirstOrDefaultAsync(cancellationToken);
        if (recipe is null || (recipe.UserId != userId && !_currentUser.IsInRole("admin")))
            throw new EntityNotFoundException("Recipe", request.RecipeId);

        var ingredients = await _context.RecipeIngredients.CountAsync(i => i.RecipeId == recipe.Id, cancellationToken);
        var plans = await _context.PlanEntries.Where(e => e.RecipeId == recipe.Id).Select(e => e.MealPlanId).Distinct().CountAsync(cancellationToken);
        var diary = await _context.FoodDiaryEntries.CountAsync(e => e.RecipeId == recipe.Id && e.UserId == userId, cancellationToken);
        var preparation = await _context.Foods.AsNoTracking()
            .Where(f => f.SourceRecipeId == recipe.Id)
            .OrderBy(f => f.Name)
            .Select(f => f.Name)
            .FirstOrDefaultAsync(cancellationToken);
        return new RecipeDeletePreview(recipe.Id, recipe.Title, ingredients, plans, diary, preparation);
    }
}
