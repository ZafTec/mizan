using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Commands;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Constants;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>
/// Writes a plan entry to the diary. The rows are built by the same factory
/// the meal log uses, carry <see cref="FoodDiaryEntry.PlanEntryId"/>, and
/// default to the entry's own date and slot. A household plan may log a recipe
/// shared in that household; a coach's draft may log the coach's own recipe.
/// A text entry has no nutrition and cannot be logged.
/// </summary>
public record LogPlanEntryCommand(Guid PlanId, Guid EntryId, DateOnly? Date, string? MealType) : IRequest<LogPlanEntryResult>;

public record LogPlanEntryResult
{
    public IReadOnlyList<Guid> DiaryEntryIds { get; init; } = [];
    public decimal Calories { get; init; }
    public decimal ProteinGrams { get; init; }
    public decimal CarbsGrams { get; init; }
    public decimal FatGrams { get; init; }
    public DateOnly Date { get; init; }
    public string MealType { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public StreakUpdate? Streak { get; init; }
    public IReadOnlyList<UnlockedAchievement> UnlockedAchievements { get; init; } = [];
}

public class LogPlanEntryCommandValidator : AbstractValidator<LogPlanEntryCommand>
{
    public LogPlanEntryCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.EntryId).NotEmpty();
        RuleFor(x => x.MealType).Must(m => MealTypes.IsValid(m)).When(x => !string.IsNullOrWhiteSpace(x.MealType))
            .WithMessage($"Meal type must be one of: {string.Join(", ", MealTypes.All)}");
    }
}

public class LogPlanEntryCommandHandler : IRequestHandler<LogPlanEntryCommand, LogPlanEntryResult>
{
    private readonly IMizanDbContext _context;
    private readonly PlanAccess _access;
    private readonly IStreakService _streaks;
    private readonly IAchievementEvaluator _achievements;
    private readonly HybridCache _cache;

    public LogPlanEntryCommandHandler(
        IMizanDbContext context, PlanAccess access, IStreakService streaks, IAchievementEvaluator achievements, HybridCache cache)
    {
        _context = context;
        _access = access;
        _streaks = streaks;
        _achievements = achievements;
        _cache = cache;
    }

    public async Task<LogPlanEntryResult> Handle(LogPlanEntryCommand request, CancellationToken cancellationToken)
    {
        var userId = _access.RequireUserId();
        var plan = await _access.LoadReadableAsync(request.PlanId, cancellationToken);
        var entry = plan.Entries.FirstOrDefault(e => e.Id == request.EntryId)
            ?? throw new EntityNotFoundException("Plan entry", request.EntryId);

        if (plan.Kind == PlanKind.Template)
            throw new DomainValidationException("A template is not followed directly. Apply it to get a plan, then log from that.");
        if (entry.Kind == PlanEntryKind.Text)
            throw new DomainValidationException("This line has no nutrition to log. Replace it with a food or a recipe first.");

        var date = request.Date
            ?? (plan.StartDate is { } start ? PlanSchedule.DateOf(start, entry.DayIndex)
                : throw new DomainValidationException("The plan has no start date, so pass the date to log on."));
        var mealType = MealTypes.Normalize(string.IsNullOrWhiteSpace(request.MealType) ? entry.Slot : request.MealType);
        var now = DateTime.UtcNow;
        var entries = new List<FoodDiaryEntry>();
        string itemName;

        if (entry.Kind == PlanEntryKind.Food)
        {
            var food = await _context.Foods
                .FirstOrDefaultAsync(f => f.Id == entry.FoodId && (f.UserId == null || f.UserId == userId || f.UserId == plan.AuthoredByUserId), cancellationToken)
                ?? throw new EntityNotFoundException("Food", entry.FoodId ?? Guid.Empty);
            itemName = food.Name;
            entries.Add(DiaryEntryFactory.FromFood(food, entry.Grams ?? 0m, userId, date, mealType, now));
        }
        else
        {
            var recipe = await _context.Recipes
                .Include(r => r.Ingredients).ThenInclude(i => i.Food)
                .FirstOrDefaultAsync(r => r.Id == entry.RecipeId && (
                    r.IsPublic || r.UserId == userId
                    || (plan.HouseholdId != null && r.HouseholdId == plan.HouseholdId)
                    || (plan.AuthoredByUserId != null && r.UserId == plan.AuthoredByUserId)), cancellationToken)
                ?? throw new EntityNotFoundException("Recipe", entry.RecipeId ?? Guid.Empty);
            itemName = recipe.Title;
            var retained = await _context.RecipeNutritionSnapshots.AsNoTracking().FirstOrDefaultAsync(s => s.RecipeId == recipe.Id, cancellationToken);
            entries.AddRange(DiaryEntryFactory.FromRecipe(recipe, entry.Servings ?? 1m, userId, date, mealType, now, retained));
        }

        foreach (var row in entries) row.PlanEntryId = entry.Id;
        _context.FoodDiaryEntries.AddRange(entries);
        await _context.SaveChangesAsync(cancellationToken);

        await _cache.RemoveByTagAsync(CacheTags.Nutrition(userId), cancellationToken);
        await _cache.RemoveByTagAsync(CacheTags.MealPlan(plan.Id), cancellationToken);
        if (entry.Kind == PlanEntryKind.Recipe)
            await _cache.RemoveByTagAsync(CacheTags.Recipes, cancellationToken);

        var streak = await _streaks.RecordActivityAsync("nutrition", date, cancellationToken);
        var unlocked = await _achievements.EvaluateAsync(cancellationToken, ["meals_logged", "streak_nutrition"]);

        return new LogPlanEntryResult
        {
            DiaryEntryIds = entries.Select(e => e.Id).ToList(),
            Calories = entries.Sum(e => e.Calories ?? 0),
            ProteinGrams = entries.Sum(e => e.ProteinGrams ?? 0),
            CarbsGrams = entries.Sum(e => e.CarbsGrams ?? 0),
            FatGrams = entries.Sum(e => e.FatGrams ?? 0),
            Date = date,
            MealType = mealType,
            Message = $"Logged {itemName} for {date:yyyy-MM-dd}.",
            Streak = streak,
            UnlockedAchievements = unlocked,
        };
    }
}
