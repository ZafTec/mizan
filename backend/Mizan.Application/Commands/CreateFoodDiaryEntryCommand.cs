using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Application.Services;
using Mizan.Domain.Constants;
using Mizan.Domain.Entities;
using Mizan.Domain.Nutrition;
using Mizan.Domain.Planning;
using Mizan.Contracts.Meals;

namespace Mizan.Application.Commands;

public record CreateFoodDiaryEntryCommand : LogMealRequest, IRequest<CreateFoodDiaryEntryResult>;

public record CreateFoodDiaryEntryResult
{
    public Guid Id { get; init; }
    public bool Success { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public StreakUpdate? Streak { get; init; }
    public IReadOnlyList<UnlockedAchievement> UnlockedAchievements { get; init; } = [];
}

public class CreateFoodDiaryEntryCommandValidator : AbstractValidator<CreateFoodDiaryEntryCommand>
{
    public CreateFoodDiaryEntryCommandValidator()
    {
        RuleFor(x => x.MealType).NotEmpty()
            .Must(m => MealTypes.IsValid(m))
            .WithMessage($"Meal type must be one of: {string.Join(", ", MealTypes.All)}");
        RuleFor(x => x.Servings).GreaterThan(0);
        RuleFor(x => x.AmountGrams).GreaterThan(0).When(x => x.AmountGrams.HasValue);
        RuleFor(x => x.Name).MaximumLength(255);
        RuleFor(x => x).Must(x => !(x.FoodId.HasValue && x.RecipeId.HasValue))
            .WithMessage("Provide a food or a recipe, not both");
        RuleFor(x => x)
            .Must(x => x.FoodId.HasValue || x.RecipeId.HasValue || !string.IsNullOrWhiteSpace(x.Name))
            .WithName("FoodId")
            .WithMessage("Either foodId, recipeId, or a meal name must be provided");
        RuleFor(x => x.Calories).GreaterThanOrEqualTo(0).When(x => x.Calories.HasValue);
        RuleFor(x => x.ProteinGrams).GreaterThanOrEqualTo(0).When(x => x.ProteinGrams.HasValue);
        RuleFor(x => x.CarbsGrams).GreaterThanOrEqualTo(0).When(x => x.CarbsGrams.HasValue);
        RuleFor(x => x.FatGrams).GreaterThanOrEqualTo(0).When(x => x.FatGrams.HasValue);
        RuleFor(x => x.FiberGrams).GreaterThanOrEqualTo(0).When(x => x.FiberGrams.HasValue);
        When(x => x.Per100g is not null, () =>
        {
            RuleFor(x => x.AmountGrams).NotNull().WithMessage("Per100g needs amountGrams to scale to");
            RuleFor(x => x.Per100g!.Calories).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Per100g!.ProteinGrams).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Per100g!.CarbsGrams).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Per100g!.FatGrams).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Per100g!.FiberGrams).GreaterThanOrEqualTo(0).When(x => x.Per100g!.FiberGrams.HasValue);
        });
        RuleFor(x => x.EntryDate)
            .Must(date => DiaryDates.Problem(date!.Value, DateOnly.FromDateTime(DateTime.UtcNow)) is null)
            .When(x => x.EntryDate.HasValue)
            .WithMessage(x => DiaryDates.Problem(x.EntryDate!.Value, DateOnly.FromDateTime(DateTime.UtcNow)) ?? string.Empty);
        RuleFor(x => x.LoggedAt)
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddMinutes(5))
            .When(x => x.LoggedAt.HasValue)
            .WithMessage("LoggedAt cannot be in the future");
        RuleFor(x => x.SharedBetween)
            .InclusiveBetween(MealShare.MinPeople, MealShare.MaxPeople)
            .When(x => x.SharedBetween.HasValue);
        RuleFor(x => x.SharedBetween)
            .Null()
            .When(x => x.RecipeId.HasValue)
            .WithMessage("A recipe is split by lowering servings, not sharedBetween");
    }
}

public class CreateFoodDiaryEntryCommandHandler : IRequestHandler<CreateFoodDiaryEntryCommand, CreateFoodDiaryEntryResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IStreakService _streakService;
    private readonly IAchievementEvaluator _achievements;
    private readonly HybridCache _cache;
    private readonly IUserClock _clock;

    public CreateFoodDiaryEntryCommandHandler(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        IStreakService streakService,
        IAchievementEvaluator achievements,
        HybridCache cache,
        IUserClock clock)
    {
        _context = context;
        _currentUser = currentUser;
        _streakService = streakService;
        _achievements = achievements;
        _cache = cache;
        _clock = clock;
    }

    public async Task<CreateFoodDiaryEntryResult> Handle(CreateFoodDiaryEntryCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return new CreateFoodDiaryEntryResult
            {
                Success = false,
                Message = "User not authenticated"
            };
        }

        var userId = _currentUser.UserId.Value;
        var loggedAt = request.LoggedAt?.ToUniversalTime() ?? DateTime.UtcNow;
        var entryDate = request.EntryDate;
        if (!entryDate.HasValue)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(await _clock.TimeZoneIdAsync(userId, cancellationToken));
            entryDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(loggedAt, zone));
        }
        Food? food = null;
        List<FoodDiaryEntry>? recipeEntries = null;
        string name = request.Name;
        decimal? calories = request.Calories, protein = request.ProteinGrams,
            carbs = request.CarbsGrams, fat = request.FatGrams, fiber = request.FiberGrams;
        if (request.Per100g is { } per100g && request.AmountGrams is { } grams)
        {
            // The server owns the arithmetic: the same rounding the food path
            // uses, from the per-100 g figures and the grams the person
            // confirmed. Totals a client sent alongside are not trusted.
            // MealScale is also what the meal form's preview shows.
            var scaled = MealScale.ForGrams(
                new EntryNutrition(per100g.Calories, per100g.ProteinGrams, per100g.CarbsGrams, per100g.FatGrams, per100g.FiberGrams ?? 0), grams);
            calories = scaled.Calories;
            protein = scaled.ProteinGrams;
            carbs = scaled.CarbsGrams;
            fat = scaled.FatGrams;
            fiber = per100g.FiberGrams.HasValue ? scaled.FiberGrams : null;
        }
        if (request.FoodId.HasValue)
        {
            food = await _context.Foods.FirstOrDefaultAsync(
                f => f.Id == request.FoodId && (f.UserId == null || f.UserId == userId), cancellationToken)
                ?? throw new EntityNotFoundException("Food", request.FoodId.Value);
            var scale = food.ServingSize * request.Servings / 100m;
            calories ??= Math.Round(food.CaloriesPer100g * scale, 2);
            protein ??= Math.Round(food.ProteinPer100g * scale, 2);
            carbs ??= Math.Round(food.CarbsPer100g * scale, 2);
            fat ??= Math.Round(food.FatPer100g * scale, 2);
            fiber ??= food.FiberPer100g.HasValue ? Math.Round(food.FiberPer100g.Value * scale, 2) : null;
            if (string.IsNullOrWhiteSpace(name)) name = food.Name;
        }
        else if (request.RecipeId.HasValue)
        {
            var recipe = await _context.Recipes.Include(r => r.Ingredients).ThenInclude(i => i.Food).FirstOrDefaultAsync(
                r => r.Id == request.RecipeId && (r.IsPublic || r.UserId == userId), cancellationToken)
                ?? throw new EntityNotFoundException("Recipe", request.RecipeId.Value);
            var retained = await _context.RecipeNutritionSnapshots.AsNoTracking().FirstOrDefaultAsync(s => s.RecipeId == recipe.Id, cancellationToken);
            recipeEntries = DiaryEntryFactory.FromRecipe(recipe, request.Servings, userId, entryDate.Value, request.MealType, loggedAt, retained);
            if (!Matches(calories, recipeEntries.Sum(e => e.Calories ?? 0))
                || !Matches(protein, recipeEntries.Sum(e => e.ProteinGrams ?? 0))
                || !Matches(carbs, recipeEntries.Sum(e => e.CarbsGrams ?? 0))
                || !Matches(fat, recipeEntries.Sum(e => e.FatGrams ?? 0))
                || !Matches(fiber, recipeEntries.Sum(e => e.FiberGrams ?? 0)))
                throw new DomainValidationException("Recipe nutrition comes from its ingredients. Adjust servings, or log a custom meal without a recipe ID.");
            if (string.IsNullOrWhiteSpace(name)) name = recipe.Title;
        }

        var amountGrams = food != null ? food.ServingSize * request.Servings : request.AmountGrams;

        // A shared meal arrives whole and is logged as one person's share. The
        // rule lives in the domain, not in whichever client asked (docs/TELEGRAM.md).
        if (request.SharedBetween is { } people)
        {
            calories = MealShare.OfCalories(calories, people);
            protein = MealShare.OfGrams(protein, people);
            carbs = MealShare.OfGrams(carbs, people);
            fat = MealShare.OfGrams(fat, people);
            fiber = MealShare.OfGrams(fiber, people);
            amountGrams = MealShare.OfGrams(amountGrams, people);
            name = MealShare.Name(name, people);
        }

        var entry = new FoodDiaryEntry
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId.Value,
            FoodId = request.FoodId,
            RecipeId = request.RecipeId,
            EntryDate = entryDate.Value,
            MealType = MealTypes.Normalize(request.MealType),
            Servings = request.Servings,
            AmountGrams = amountGrams,
            Calories = calories,
            ProteinGrams = protein,
            CarbsGrams = carbs,
            FatGrams = fat,
            FiberGrams = fiber,
            ProteinCalorieRatio = Food.ComputeProteinCalorieRatio(calories ?? 0, protein ?? 0),
            Name = name,
            LoggedAt = loggedAt
        };

        var entries = recipeEntries ?? [entry];
        _context.FoodDiaryEntries.AddRange(entries);
        await _context.SaveChangesAsync(cancellationToken);

        await _cache.RemoveByTagAsync(CacheTags.Nutrition(_currentUser.UserId.Value), cancellationToken);

        if (request.RecipeId.HasValue)
            await _cache.RemoveByTagAsync(CacheTags.Recipes, cancellationToken);

        var streak = await _streakService.RecordActivityAsync("nutrition", entryDate, cancellationToken);
        var unlocked = await _achievements.EvaluateAsync(cancellationToken, ["meals_logged", "streak_nutrition"]);

        var warnings = NutritionHints.CheckConsistency(
            request.Per100g is null ? request.Calories : calories,
            request.Per100g is null ? request.ProteinGrams : protein,
            request.Per100g is null ? request.CarbsGrams : carbs,
            request.Per100g is null ? request.FatGrams : fat,
            request.Per100g is null ? request.FiberGrams : fiber);

        return new CreateFoodDiaryEntryResult
        {
            Id = entries[0].Id,
            Success = true,
            Message = "Entry logged successfully",
            Warnings = warnings,
            Streak = streak,
            UnlockedAchievements = unlocked
        };
    }

    private static bool Matches(decimal? supplied, decimal calculated) =>
        !supplied.HasValue || Math.Abs(supplied.Value - calculated) <= 0.02m;
}
