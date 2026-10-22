using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;
using Mizan.Domain.Recipes;

namespace Mizan.Application.Planning;

/// <summary>
/// Builds the plan a surface shows: entries by day and slot with resolved
/// nutrition, day totals from <see cref="PlanNutrition"/>, violations from
/// <see cref="PlanRuleCheck"/>, and each entry's logged flag from the viewer's
/// diary. Recipe nutrition comes from <see cref="RecipeNutritionLookup"/>, so a
/// retained snapshot stands in exactly as it does on the recipe page.
/// </summary>
public sealed class PlanReadModel
{
    private readonly IMizanDbContext _context;

    public PlanReadModel(IMizanDbContext context) => _context = context;

    public async Task<MealPlanDetailDto> BuildAsync(MealPlan plan, Guid viewerId, bool canEdit, CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(plan, viewerId, cancellationToken);
        var days = Enumerable.Range(0, plan.DurationDays)
            .Select(day => BuildDay(plan, day, resolved))
            .ToList();

        return new MealPlanDetailDto
        {
            Id = plan.Id,
            Name = plan.Name,
            Description = plan.Description,
            Kind = plan.Kind.ToString(),
            Status = plan.Status.ToString(),
            StartDate = plan.StartDate,
            EndDate = plan.EndDate,
            DurationDays = plan.DurationDays,
            UserId = plan.UserId,
            HouseholdId = plan.HouseholdId,
            AuthoredByUserId = plan.AuthoredByUserId,
            AuthoredByName = resolved.AuthoredByName,
            Rules = PlanRulesMapping.ToDto(plan.Rules),
            Days = days,
            PrepSessions = plan.PrepSessions
                .OrderBy(s => s.DayIndex)
                .Select(s => new PrepSessionDto(
                    s.Id, s.DayIndex, DateOf(plan, s.DayIndex), s.Title, s.Notes,
                    plan.Entries.Count(e => e.PrepSessionId == s.Id)))
                .ToList(),
            Violations = resolved.Violations.Select(ToDto).ToList(),
            EntryCount = plan.Entries.Count,
            LoggedCount = days.Sum(d => d.LoggedCount),
            CanEdit = canEdit,
            WeeklyCost = null,
            CreatedAt = plan.CreatedAt,
            UpdatedAt = plan.UpdatedAt,
        };
    }

    public async Task<PlanDayDto> BuildDayAsync(MealPlan plan, int dayIndex, Guid viewerId, CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(plan, viewerId, cancellationToken);
        return BuildDay(plan, dayIndex, resolved);
    }

    private static PlanDayDto BuildDay(MealPlan plan, int dayIndex, Resolved resolved)
    {
        var dayEntries = plan.Entries.Where(e => e.DayIndex == dayIndex).ToList();
        var slots = PlanSlots.All
            .Select(slot => new PlanSlotDto(slot, dayEntries
                .Where(e => PlanSlots.Normalize(e.Slot) == slot)
                .OrderBy(e => e.SortOrder)
                .Select(e => ToDto(plan, e, resolved))
                .ToList()))
            .Where(s => s.Entries.Count > 0)
            .ToList();
        var nutrition = resolved.Days.FirstOrDefault(d => d.DayIndex == dayIndex);
        var totals = nutrition?.Totals ?? EntryNutrition.Zero;
        return new PlanDayDto
        {
            DayIndex = dayIndex,
            Date = DateOf(plan, dayIndex),
            Slots = slots,
            Totals = new EntryNutritionDto(totals.Calories, totals.ProteinGrams, totals.CarbsGrams, totals.FatGrams, totals.FiberGrams),
            UnresolvedCount = nutrition?.UnresolvedEntryIds.Count ?? 0,
            EntryCount = dayEntries.Count,
            LoggedCount = dayEntries.Count(e => resolved.Logged.Contains(e.Id)),
            Violations = resolved.Violations.Where(v => v.DayIndex == dayIndex).Select(ToDto).ToList(),
            Cost = null,
        };
    }

    private static PlanEntryDto ToDto(MealPlan plan, PlanEntry entry, Resolved resolved)
    {
        var nutrition = PlanNutrition.Of(entry, resolved.RecipePerServing, resolved.Foods);
        resolved.Recipes.TryGetValue(entry.RecipeId ?? Guid.Empty, out var recipe);
        resolved.Foods.TryGetValue(entry.FoodId ?? Guid.Empty, out var food);
        return new PlanEntryDto
        {
            Id = entry.Id,
            DayIndex = entry.DayIndex,
            Date = DateOf(plan, entry.DayIndex),
            Slot = PlanSlots.Normalize(entry.Slot),
            Kind = entry.Kind.ToString(),
            RecipeId = entry.RecipeId,
            RecipeTitle = recipe?.Title,
            RecipeImageUrl = recipe?.ImageUrl,
            Servings = entry.Servings,
            FoodId = entry.FoodId,
            FoodName = food?.Name,
            Grams = entry.Grams,
            Text = entry.Text,
            Notes = entry.Notes,
            SortOrder = entry.SortOrder,
            PrepSessionId = entry.PrepSessionId,
            Nutrition = nutrition is { } n
                ? new EntryNutritionDto(Math.Round(n.Calories, 1), Math.Round(n.ProteinGrams, 1), Math.Round(n.CarbsGrams, 1), Math.Round(n.FatGrams, 1), Math.Round(n.FiberGrams, 1))
                : null,
            Logged = resolved.Logged.Contains(entry.Id),
            Cost = null,
        };
    }

    private static PlanViolationDto ToDto(PlanViolation v) => new(v.DayIndex, v.Slot, v.Rule, v.Message);

    private static DateOnly? DateOf(MealPlan plan, int dayIndex) =>
        plan.StartDate is { } start ? PlanSchedule.DateOf(start, dayIndex) : null;

    private async Task<Resolved> ResolveAsync(MealPlan plan, Guid viewerId, CancellationToken cancellationToken)
    {
        var recipeIds = plan.Entries.Where(e => e.RecipeId.HasValue).Select(e => e.RecipeId!.Value).Distinct().ToList();
        var recipes = recipeIds.Count == 0
            ? new Dictionary<Guid, RecipeHead>()
            : await _context.Recipes.AsNoTracking()
                .Where(r => recipeIds.Contains(r.Id))
                .Select(r => new RecipeHead(r.Id, r.Title, r.ImageUrl, r.Ingredients.Where(i => i.FoodId != null).Select(i => i.FoodId!.Value).ToList()))
                .ToDictionaryAsync(r => r.Id, cancellationToken);
        var perServing = await RecipeNutritionLookup.ForRecipesAsync(_context, recipeIds, cancellationToken);

        var foodIds = plan.Entries.Where(e => e.FoodId.HasValue).Select(e => e.FoodId!.Value)
            .Concat(recipes.Values.SelectMany(r => r.FoodIds))
            .Distinct()
            .ToList();
        var foods = foodIds.Count == 0
            ? new Dictionary<Guid, Food>()
            : await _context.Foods.AsNoTracking().Where(f => foodIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, cancellationToken);

        var logged = new HashSet<Guid>();
        if (plan.StartDate is { } start && plan.Entries.Count > 0)
        {
            var entryIds = plan.Entries.Select(e => e.Id).ToList();
            var rows = await _context.FoodDiaryEntries.AsNoTracking()
                .Where(d => d.UserId == viewerId && d.PlanEntryId != null && entryIds.Contains(d.PlanEntryId.Value))
                .Select(d => new { EntryId = d.PlanEntryId!.Value, d.EntryDate })
                .ToListAsync(cancellationToken);
            var dateByEntry = plan.Entries.ToDictionary(e => e.Id, e => PlanSchedule.DateOf(start, e.DayIndex));
            foreach (var row in rows)
                if (dateByEntry.TryGetValue(row.EntryId, out var date) && date == row.EntryDate) logged.Add(row.EntryId);
        }

        var entries = plan.Entries.ToList();
        var days = PlanNutrition.ByDay(entries, plan.DurationDays, perServing, foods);
        var foodIdsByEntry = entries.ToDictionary(
            e => e.Id,
            e => (IReadOnlyList<Guid>)(e.Kind switch
            {
                PlanEntryKind.Food when e.FoodId is { } f => [f],
                PlanEntryKind.Recipe when e.RecipeId is { } r && recipes.TryGetValue(r, out var head) => head.FoodIds,
                _ => [],
            }));
        var foodNames = foods.ToDictionary(f => f.Key, f => f.Value.Name);
        var violations = PlanRuleCheck.Evaluate(plan.Rules, entries, days, foodIdsByEntry, foodNames);

        string? authorName = null;
        if (plan.AuthoredByUserId is { } authorId)
            authorName = await _context.Users.AsNoTracking().Where(u => u.Id == authorId).Select(u => u.Name).FirstOrDefaultAsync(cancellationToken);

        return new Resolved(recipes, perServing, foods, logged, days, violations, authorName);
    }

    private sealed record RecipeHead(Guid Id, string Title, string? ImageUrl, List<Guid> FoodIds);

    private sealed record Resolved(
        IReadOnlyDictionary<Guid, RecipeHead> Recipes,
        IReadOnlyDictionary<Guid, RecipeNutritionTotals> RecipePerServing,
        IReadOnlyDictionary<Guid, Food> Foods,
        HashSet<Guid> Logged,
        IReadOnlyList<PlanDayNutrition> Days,
        IReadOnlyList<PlanViolation> Violations,
        string? AuthoredByName);
}
