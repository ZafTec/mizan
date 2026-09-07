using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Constants;
using Mizan.Domain.Entities;
using Mizan.Domain.Recipes;

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
        RuleFor(x => x).Must(x => x.FoodId.HasValue != x.RecipeId.HasValue)
            .WithMessage("Exactly one of FoodId or RecipeId must be provided");
    }
}

public class LogFoodCommandHandler : IRequestHandler<LogFoodCommand, LogFoodResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IStreakService _streakService;
    private readonly IAchievementEvaluator _achievements;
    private readonly HybridCache _cache;

    public LogFoodCommandHandler(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        IStreakService streakService,
        IAchievementEvaluator achievements,
        HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _streakService = streakService;
        _achievements = achievements;
        _cache = cache;
    }

    public async Task<LogFoodResult> Handle(LogFoodCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var userId = _currentUser.UserId.Value;
        var entries = new List<FoodDiaryEntry>();
        var now = DateTime.UtcNow;
        string itemName;

        if (request.FoodId.HasValue)
        {
            var food = await _context.Foods.FirstOrDefaultAsync(
                f => f.Id == request.FoodId && (f.UserId == null || f.UserId == userId), cancellationToken)
                ?? throw new EntityNotFoundException("Food", request.FoodId.Value);
            itemName = food.Name;
            entries.Add(DiaryEntryFactory.FromFood(food, food.ServingSize * request.Servings, userId, request.EntryDate, request.MealType, now));
        }
        else if (request.RecipeId.HasValue)
        {
            var recipe = await _context.Recipes
                .Include(r => r.Ingredients).ThenInclude(i => i.Food)
                .FirstOrDefaultAsync(r => r.Id == request.RecipeId.Value && (r.IsPublic || r.UserId == userId), cancellationToken)
                ?? throw new EntityNotFoundException("Recipe", request.RecipeId.Value);
            itemName = recipe.Title;
            entries.AddRange(DiaryEntryFactory.FromRecipe(recipe, request.Servings, userId, request.EntryDate, request.MealType, now));
        }
        else throw new DomainValidationException("Exactly one of FoodId or RecipeId must be provided");

        _context.FoodDiaryEntries.AddRange(entries);
        await _context.SaveChangesAsync(cancellationToken);

        // Never a Postgres round trip, so it does not touch the logging budget
        // (docs/ARCHITECTURE.md#navigation-and-logging) - only the Redis-backed nutrition cache.
        await _cache.RemoveByTagAsync(CacheTags.Nutrition(_currentUser.UserId.Value), cancellationToken);
        if (request.RecipeId.HasValue)
            await _cache.RemoveByTagAsync(CacheTags.Recipes, cancellationToken);

        var streak = await _streakService.RecordActivityAsync("nutrition", request.EntryDate, cancellationToken);
        var unlocked = await _achievements.EvaluateAsync(cancellationToken, ["meals_logged", "streak_nutrition"]);

        return new LogFoodResult
        {
            Id = entries[0].Id,
            Calories = entries.Sum(e => e.Calories ?? 0),
            ProteinGrams = entries.Sum(e => e.ProteinGrams ?? 0),
            CarbsGrams = entries.Sum(e => e.CarbsGrams ?? 0),
            FatGrams = entries.Sum(e => e.FatGrams ?? 0),
            Message = $"Logged {request.Servings} serving(s) of {itemName}",
            Streak = streak,
            UnlockedAchievements = unlocked
        };

    }
}
