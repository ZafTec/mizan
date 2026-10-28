using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Application.Prices;
using Mizan.Contracts.Planning;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;
using Mizan.Domain.Pricing;
using Mizan.Domain.Recipes;

namespace Mizan.Application.Planning;

/// <summary>
/// Builds the plan a surface shows: entries by day and slot with resolved
/// nutrition, day totals from <see cref="PlanNutrition"/>, violations from
/// <see cref="PlanRuleCheck"/>, and each entry's logged flag from the viewer's
/// diary. Recipe nutrition comes from <see cref="RecipeNutritionLookup"/>, so a
/// retained snapshot stands in exactly as it does on the recipe page. Costs
/// come from <see cref="PlanCosting"/> at the viewer's region prices.
/// </summary>
public sealed class PlanReadModel
{
    private readonly IMizanDbContext _context;
    private readonly PriceBookReader _prices;
    private readonly PlanTargetsReader _targets;

    public PlanReadModel(IMizanDbContext context, PriceBookReader prices, PlanTargetsReader targets)
    {
        _context = context;
        _prices = prices;
        _targets = targets;
    }

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
            EffectiveTargets = PlanTargetsReader.ToDto(plan.Rules, resolved.Targets),
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
            Currency = resolved.Currency,
            TotalCost = resolved.Cost.TotalCost,
            WeeklyCost = resolved.Cost.WeeklyCost,
            OverBudget = PlanCosting.OverBudget(resolved.Cost.WeeklyCost, plan.Rules.WeeklyBudget, plan.Rules.Currency, resolved.Currency),
            CostCoverage = resolved.Cost.Summary.Coverage,
            CostConfidence = resolved.Cost.Summary.LowestConfidence?.ToString(),
            UnpricedItems = resolved.Cost.Summary.Unpriced.Select(i => new UnpricedItemDto(i.Name, i.FoodId, i.Reason ?? string.Empty)).ToList(),
            GeneratedFrom = PlanGenerationMapping.ToDto(plan.GeneratedFrom),
            CreatedAt = plan.CreatedAt,
            UpdatedAt = plan.UpdatedAt,
        };
    }

    /// <summary>One day of the plan, the currency its costs are in, and the targets it is held to.</summary>
    public async Task<(PlanDayDto Day, string Currency, PlanTargetsDto Targets)> BuildDayAsync(MealPlan plan, int dayIndex, Guid viewerId, CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(plan, viewerId, cancellationToken);
        return (BuildDay(plan, dayIndex, resolved), resolved.Currency, PlanTargetsReader.ToDto(plan.Rules, resolved.Targets));
    }

    private static PlanDayDto BuildDay(MealPlan plan, int dayIndex, Resolved resolved)
    {
        var dayEntries = plan.Entries.Where(e => e.DayIndex == dayIndex).ToList();
        var slots = PlanSlots.All
            .Select(slot => dayEntries.Where(e => PlanSlots.Normalize(e.Slot) == slot).OrderBy(e => e.SortOrder).ToList())
            .Where(entries => entries.Count > 0)
            .Select(entries => new PlanSlotDto(
                PlanSlots.Normalize(entries[0].Slot),
                entries.Select(e => ToDto(plan, e, resolved)).ToList(),
                ToDto(PlanNutrition.Sum(entries, resolved.RecipePerServing, resolved.Foods))))
            .ToList();
        var nutrition = resolved.Days.FirstOrDefault(d => d.DayIndex == dayIndex);
        var totals = nutrition?.Totals ?? EntryNutrition.Zero;
        return new PlanDayDto
        {
            DayIndex = dayIndex,
            Date = DateOf(plan, dayIndex),
            Slots = slots,
            Totals = ToDto(totals),
            UnresolvedCount = nutrition?.UnresolvedEntryIds.Count ?? 0,
            EntryCount = dayEntries.Count,
            LoggedCount = dayEntries.Count(e => resolved.Logged.Contains(e.Id)),
            Violations = resolved.Violations.Where(v => v.DayIndex == dayIndex).Select(ToDto).ToList(),
            Cost = resolved.Cost.DayCosts.GetValueOrDefault(dayIndex),
            OverBudget = PlanCosting.OverBudget(
                resolved.Cost.DayCosts.GetValueOrDefault(dayIndex), plan.Rules.DailyBudget, plan.Rules.Currency, resolved.Currency),
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
            Cost = resolved.Cost.EntryCosts.GetValueOrDefault(entry.Id),
        };
    }

    private static EntryNutritionDto ToDto(EntryNutrition n) => new(n.Calories, n.ProteinGrams, n.CarbsGrams, n.FatGrams, n.FiberGrams);

    private static PlanViolationDto ToDto(PlanViolation v) => new(v.DayIndex, v.Slot, v.Rule, v.Message);

    private static DateOnly? DateOf(MealPlan plan, int dayIndex) =>
        plan.StartDate is { } start ? PlanSchedule.DateOf(start, dayIndex) : null;

    private async Task<Resolved> ResolveAsync(MealPlan plan, Guid viewerId, CancellationToken cancellationToken)
    {
        var recipeIds = plan.Entries.Where(e => e.RecipeId.HasValue).Select(e => e.RecipeId!.Value).Distinct().ToList();
        // Whole recipes with their lines: the cost scales each line the way the shopping list does.
        var fullRecipes = recipeIds.Count == 0
            ? new Dictionary<Guid, Recipe>()
            : await _context.Recipes.AsNoTracking().Include(r => r.Ingredients)
                .Where(r => recipeIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, cancellationToken);
        var recipes = fullRecipes.Values.ToDictionary(
            r => r.Id,
            r => new RecipeHead(r.Id, r.Title, r.ImageUrl, r.Ingredients.Where(i => i.FoodId != null).Select(i => i.FoodId!.Value).ToList()));
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

        var region = await _prices.RegionForAsync(viewerId, cancellationToken);
        var quotes = await _prices.QuotesAsync(foods.Values, region, cancellationToken);
        var cost = PlanCosting.Cost(entries, plan.DurationDays, fullRecipes, foods, quotes, region.Currency);

        // The budget rules compare like with like: a budget in another currency is not checked.
        var sameCurrency = string.Equals(plan.Rules.Currency, region.Currency, StringComparison.OrdinalIgnoreCase);
        var targets = await _targets.ForPlanAsync(plan, viewerId, cancellationToken);
        var violations = PlanRuleCheck.Evaluate(
            plan.Rules, entries, days, foodIdsByEntry, foodNames,
            sameCurrency ? cost.WeeklyCost : null,
            sameCurrency ? cost.DayCosts : null,
            targets);

        string? authorName = null;
        if (plan.AuthoredByUserId is { } authorId)
            authorName = await _context.Users.AsNoTracking().Where(u => u.Id == authorId).Select(u => u.Name).FirstOrDefaultAsync(cancellationToken);

        return new Resolved(recipes, perServing, foods, logged, days, violations, authorName, cost, region.Currency, targets);
    }

    private sealed record RecipeHead(Guid Id, string Title, string? ImageUrl, List<Guid> FoodIds);

    private sealed record Resolved(
        IReadOnlyDictionary<Guid, RecipeHead> Recipes,
        IReadOnlyDictionary<Guid, RecipeNutritionTotals> RecipePerServing,
        IReadOnlyDictionary<Guid, Food> Foods,
        HashSet<Guid> Logged,
        IReadOnlyList<PlanDayNutrition> Days,
        IReadOnlyList<PlanViolation> Violations,
        string? AuthoredByName,
        PlanCost Cost,
        string Currency,
        PlanTargets Targets);
}
