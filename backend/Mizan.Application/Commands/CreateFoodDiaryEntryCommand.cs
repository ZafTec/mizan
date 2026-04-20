using FluentValidation;
using MediatR;
using Mizan.Application.Interfaces;
using Mizan.Application.Services;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record CreateFoodDiaryEntryCommand : IRequest<CreateFoodDiaryEntryResult>
{
    public Guid? FoodId { get; init; }
    public Guid? RecipeId { get; init; }
    public DateOnly? EntryDate { get; init; }
    /// <summary>
    /// Optional precise timestamp of when the meal was eaten. Lets callers
    /// (MCP, frontend, apps) backfill entries at a specific time.
    /// Stored as UTC; falls back to <c>DateTime.UtcNow</c> when omitted.
    /// </summary>
    public DateTime? LoggedAt { get; init; }
    public string MealType { get; init; } = "SNACK";
    public decimal Servings { get; init; } = 1;
    public decimal? Calories { get; init; }
    public decimal? ProteinGrams { get; init; }
    public decimal? CarbsGrams { get; init; }
    public decimal? FatGrams { get; init; }
    public decimal? FiberGrams { get; init; }
    public string Name { get; init; } = string.Empty;
}

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
            .Must(m => new[] { "MEAL", "SNACK", "DRINK" }.Contains(m.ToUpper()))
            .WithMessage("Meal type must be MEAL, SNACK, or DRINK");
        RuleFor(x => x.Servings).GreaterThan(0);
        RuleFor(x => x)
            .Must(x => x.FoodId.HasValue || x.RecipeId.HasValue || !string.IsNullOrWhiteSpace(x.Name))
            .WithName("FoodId")
            .WithMessage("Either foodId, recipeId, or a meal name must be provided");
        RuleFor(x => x.Calories).GreaterThanOrEqualTo(0).When(x => x.Calories.HasValue);
        RuleFor(x => x.ProteinGrams).GreaterThanOrEqualTo(0).When(x => x.ProteinGrams.HasValue);
        RuleFor(x => x.CarbsGrams).GreaterThanOrEqualTo(0).When(x => x.CarbsGrams.HasValue);
        RuleFor(x => x.FatGrams).GreaterThanOrEqualTo(0).When(x => x.FatGrams.HasValue);
        RuleFor(x => x.FiberGrams).GreaterThanOrEqualTo(0).When(x => x.FiberGrams.HasValue);
        RuleFor(x => x.LoggedAt)
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddMinutes(5))
            .When(x => x.LoggedAt.HasValue)
            .WithMessage("LoggedAt cannot be in the future");
    }
}

public class CreateFoodDiaryEntryCommandHandler : IRequestHandler<CreateFoodDiaryEntryCommand, CreateFoodDiaryEntryResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IStreakService _streakService;
    private readonly IAchievementEvaluator _achievements;

    public CreateFoodDiaryEntryCommandHandler(
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

        // LoggedAt precedence: explicit client value > now.
        // EntryDate precedence: explicit > derived from LoggedAt > today.
        // Stored UTC so range queries are timezone-deterministic.
        var loggedAt = request.LoggedAt?.ToUniversalTime() ?? DateTime.UtcNow;
        var entryDate = request.EntryDate
            ?? (request.LoggedAt.HasValue
                ? DateOnly.FromDateTime(loggedAt)
                : DateOnly.FromDateTime(DateTime.UtcNow));

        var entry = new FoodDiaryEntry
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId.Value,
            FoodId = request.FoodId,
            RecipeId = request.RecipeId,
            EntryDate = entryDate,
            MealType = request.MealType.ToUpper(),
            Servings = request.Servings,
            Calories = request.Calories,
            ProteinGrams = request.ProteinGrams,
            CarbsGrams = request.CarbsGrams,
            FatGrams = request.FatGrams,
            FiberGrams = request.FiberGrams,
            ProteinCalorieRatio = Food.ComputeProteinCalorieRatio(request.Calories ?? 0, request.ProteinGrams ?? 0),
            Name = request.Name,
            LoggedAt = loggedAt
        };

        _context.FoodDiaryEntries.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);

        var streak = await _streakService.RecordActivityAsync("nutrition", cancellationToken);
        var unlocked = await _achievements.EvaluateAsync(cancellationToken);

        var warnings = NutritionHints.CheckConsistency(
            request.Calories,
            request.ProteinGrams,
            request.CarbsGrams,
            request.FatGrams,
            request.FiberGrams);

        return new CreateFoodDiaryEntryResult
        {
            Id = entry.Id,
            Success = true,
            Message = "Entry logged successfully",
            Warnings = warnings,
            Streak = streak,
            UnlockedAchievements = unlocked
        };
    }
}
