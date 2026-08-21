using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Domain.Constants;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record LogFoodCommand : IRequest<LogFoodResult>
{
    public Guid? FoodId { get; init; }
    public Guid? RecipeId { get; init; }
    public DateOnly EntryDate { get; init; }
    public string MealType { get; init; } = MealTypes.Snack;
    public decimal Servings { get; init; } = 1;
}

public record LogFoodResult
{
    public Guid Id { get; init; }
    public decimal Calories { get; init; }
    public decimal ProteinGrams { get; init; }
    public decimal CarbsGrams { get; init; }
    public decimal FatGrams { get; init; }
    public string Message { get; init; } = string.Empty;
    public StreakUpdate? Streak { get; init; }
    public IReadOnlyList<UnlockedAchievement> UnlockedAchievements { get; init; } = [];
}

public class LogFoodCommandValidator : AbstractValidator<LogFoodCommand>
{
    public LogFoodCommandValidator()
    {
        RuleFor(x => x.Servings).GreaterThan(0).WithMessage("Servings must be greater than 0");
        RuleFor(x => x.MealType).Must(x => MealTypes.IsValid(x))
            .WithMessage($"Meal type must be one of: {string.Join(", ", MealTypes.All)}");
        RuleFor(x => x).Must(x => x.FoodId.HasValue || x.RecipeId.HasValue)
            .WithMessage("Either FoodId or RecipeId must be provided");
    }
}

public class LogFoodCommandHandler : IRequestHandler<LogFoodCommand, LogFoodResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IStreakService _streakService;
    private readonly IAchievementEvaluator _achievements;

    public LogFoodCommandHandler(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        IStreakService streakService,
        IAchievementEvaluator achievements)
    {
        _context = context;
        _currentUser = currentUser;
        _streakService = streakService;
        _achievements = achievements;
    }

    public async Task<LogFoodResult> Handle(LogFoodCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        decimal calories = 0;
        decimal protein = 0, carbs = 0, fat = 0;
        string itemName = "";

        if (request.FoodId.HasValue)
        {
            var food = await _context.Foods.FindAsync(new object[] { request.FoodId.Value }, cancellationToken);
            if (food != null)
            {
                calories = food.CaloriesPer100g * (food.ServingSize / 100m) * request.Servings;
                protein = food.ProteinPer100g * (food.ServingSize / 100m) * request.Servings;
                carbs = food.CarbsPer100g * (food.ServingSize / 100m) * request.Servings;
                fat = food.FatPer100g * (food.ServingSize / 100m) * request.Servings;
                itemName = food.Name;
            }
        }
        else if (request.RecipeId.HasValue)
        {
            var recipe = await _context.Recipes
                .FirstOrDefaultAsync(r => r.Id == request.RecipeId.Value, cancellationToken);

            if (recipe != null)
            {
                // Summed from the ingredients rather than read from a stored
                // table - see docs/REFOCUS.md §4. The result is copied onto the
                // entry below, so the log keeps the macros it was written with
                // even if the recipe later changes.
                var totals = await RecipeNutritionLookup.ForRecipeAsync(
                    _context, recipe.Id, cancellationToken);

                calories = totals.Calories * request.Servings;
                protein = totals.ProteinGrams * request.Servings;
                carbs = totals.CarbsGrams * request.Servings;
                fat = totals.FatGrams * request.Servings;
                itemName = recipe.Title;
            }
        }

        var entry = new FoodDiaryEntry
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId.Value,
            FoodId = request.FoodId,
            RecipeId = request.RecipeId,
            EntryDate = request.EntryDate,
            MealType = MealTypes.Normalize(request.MealType),
            Servings = request.Servings,
            Calories = calories,
            ProteinGrams = protein,
            CarbsGrams = carbs,
            FatGrams = fat,
            ProteinCalorieRatio = Food.ComputeProteinCalorieRatio(calories, protein),
            LoggedAt = DateTime.UtcNow
        };

        _context.FoodDiaryEntries.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);

        var streak = await _streakService.RecordActivityAsync("nutrition", request.EntryDate, cancellationToken);
        var unlocked = await _achievements.EvaluateAsync(cancellationToken, ["meals_logged", "streak_nutrition"]);

        return new LogFoodResult
        {
            Id = entry.Id,
            Calories = calories,
            ProteinGrams = protein,
            CarbsGrams = carbs,
            FatGrams = fat,
            Message = $"Logged {request.Servings} serving(s) of {itemName} ({calories} kcal)",
            Streak = streak,
            UnlockedAchievements = unlocked
        };
    }
}
