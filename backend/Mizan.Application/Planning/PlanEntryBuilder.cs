using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>
/// Turns entry requests into <see cref="PlanEntry"/> rows. The shape of each
/// request is checked by its validator; this checks what needs the database:
/// the day fits the plan, the recipe is public, the caller's own, or shared in
/// a household the caller can see, the food is public or the caller's own, and
/// a prep session belongs to the same plan.
/// </summary>
public sealed class PlanEntryBuilder
{
    private readonly IMizanDbContext _context;
    private readonly IHouseholdAccess _households;
    private readonly ICurrentUserService _currentUser;

    public PlanEntryBuilder(IMizanDbContext context, IHouseholdAccess households, ICurrentUserService currentUser)
    {
        _context = context;
        _households = households;
        _currentUser = currentUser;
    }

    /// <param name="sessionIdsByIndex">On a create, the ids of the sessions the request listed, by position.</param>
    public async Task<List<PlanEntry>> BuildAsync(
        MealPlan plan,
        IReadOnlyList<PlanEntryRequest> requests,
        IReadOnlyList<Guid>? sessionIdsByIndex,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User must be authenticated");
        if (requests.Count == 0) return [];

        var recipeIds = requests.Where(r => Kind(r) == PlanEntryKind.Recipe && r.RecipeId is not null).Select(r => r.RecipeId!.Value).Distinct().ToList();
        var foodIds = requests.Where(r => Kind(r) == PlanEntryKind.Food && r.FoodId is not null).Select(r => r.FoodId!.Value).Distinct().ToList();
        await EnsureRecipesUsableAsync(userId, recipeIds, cancellationToken);
        await EnsureFoodsUsableAsync(userId, foodIds, cancellationToken);

        var sessionIds = plan.PrepSessions.Select(s => s.Id).ToHashSet();
        var entries = new List<PlanEntry>(requests.Count);
        foreach (var request in requests)
        {
            var entry = new PlanEntry { Id = Guid.NewGuid(), MealPlanId = plan.Id };
            Apply(plan, entry, request, sessionIdsByIndex, sessionIds);
            entries.Add(entry);
        }
        return entries;
    }

    /// <summary>Rewrites one existing entry from a request, with the same checks as a new one.</summary>
    public async Task ApplyAsync(MealPlan plan, PlanEntry entry, PlanEntryRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User must be authenticated");
        var kind = Kind(request);
        if (kind == PlanEntryKind.Recipe && request.RecipeId is { } recipeId)
            await EnsureRecipesUsableAsync(userId, [recipeId], cancellationToken);
        if (kind == PlanEntryKind.Food && request.FoodId is { } foodId)
            await EnsureFoodsUsableAsync(userId, [foodId], cancellationToken);
        Apply(plan, entry, request, null, plan.PrepSessions.Select(s => s.Id).ToHashSet());
    }

    private static void Apply(MealPlan plan, PlanEntry entry, PlanEntryRequest request, IReadOnlyList<Guid>? sessionIdsByIndex, HashSet<Guid> sessionIds)
    {
        if (request.DayIndex < 0 || request.DayIndex >= plan.DurationDays)
            throw new DomainValidationException($"Day {request.DayIndex} is outside the plan, which has {plan.DurationDays} day(s) counted from 0.");

        var kind = Kind(request);
        entry.DayIndex = request.DayIndex;
        entry.Slot = PlanSlots.Normalize(request.Slot);
        entry.Kind = kind;
        entry.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        entry.SortOrder = request.SortOrder;
        entry.RecipeId = kind == PlanEntryKind.Recipe ? request.RecipeId : null;
        entry.Servings = kind == PlanEntryKind.Recipe ? request.Servings : null;
        entry.FoodId = kind == PlanEntryKind.Food ? request.FoodId : null;
        entry.Grams = kind == PlanEntryKind.Food ? request.Grams : null;
        entry.Text = kind == PlanEntryKind.Text ? request.Text!.Trim() : null;

        Guid? session = null;
        if (request.PrepSessionIndex is { } index && sessionIdsByIndex is not null)
        {
            if (index < 0 || index >= sessionIdsByIndex.Count)
                throw new DomainValidationException($"Prep session {index} is not in the request.");
            session = sessionIdsByIndex[index];
        }
        else if (request.PrepSessionId is { } sessionId)
        {
            if (!sessionIds.Contains(sessionId))
                throw new DomainValidationException("The prep session does not belong to this plan.");
            session = sessionId;
        }
        entry.PrepSessionId = session;
    }

    private static PlanEntryKind Kind(PlanEntryRequest request) =>
        PlanRulesMapping.ParseEnum(request.Kind, PlanEntryKind.Recipe);

    private async Task EnsureRecipesUsableAsync(Guid userId, IReadOnlyCollection<Guid> recipeIds, CancellationToken cancellationToken)
    {
        if (recipeIds.Count == 0) return;
        var accessibleHouseholds = await _households.AccessibleIdsAsync(cancellationToken);
        var usable = await _context.Recipes
            .Where(recipe => recipeIds.Contains(recipe.Id) &&
                (recipe.IsPublic || recipe.UserId == userId ||
                 (recipe.HouseholdId.HasValue && accessibleHouseholds.Contains(recipe.HouseholdId.Value))))
            .Select(recipe => recipe.Id)
            .ToListAsync(cancellationToken);
        var missing = recipeIds.Except(usable).ToList();
        if (missing.Count > 0)
            throw new DomainValidationException($"Recipe {missing[0]} was not found or you cannot use it in a plan.");
    }

    private async Task EnsureFoodsUsableAsync(Guid userId, IReadOnlyCollection<Guid> foodIds, CancellationToken cancellationToken)
    {
        if (foodIds.Count == 0) return;
        var usable = await _context.Foods
            .Where(food => foodIds.Contains(food.Id) && (food.UserId == null || food.UserId == userId))
            .Select(food => food.Id)
            .ToListAsync(cancellationToken);
        var missing = foodIds.Except(usable).ToList();
        if (missing.Count > 0)
            throw new DomainValidationException($"Food {missing[0]} was not found or you cannot use it in a plan.");
    }
}
