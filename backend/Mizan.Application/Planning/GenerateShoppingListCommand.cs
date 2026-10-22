using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>
/// Builds a shopping list from a plan with <see cref="ShoppingAggregator"/>:
/// recipe lines scaled by servings, grams summed per food, unconvertible lines
/// and text entries kept for review, grouped by prep session. A new list is
/// created, or an existing list's items are replaced.
/// </summary>
public record GenerateShoppingListCommand(Guid PlanId, string? Name, Guid? ReplaceListId) : IRequest<GenerateShoppingListResult>;

public class GenerateShoppingListCommandValidator : AbstractValidator<GenerateShoppingListCommand>
{
    public GenerateShoppingListCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Name).MaximumLength(255);
    }
}

public class GenerateShoppingListCommandHandler : IRequestHandler<GenerateShoppingListCommand, GenerateShoppingListResult>
{
    private readonly IMizanDbContext _context;
    private readonly PlanAccess _plans;
    private readonly ShoppingListAccess _lists;
    private readonly HybridCache _cache;

    public GenerateShoppingListCommandHandler(IMizanDbContext context, PlanAccess plans, ShoppingListAccess lists, HybridCache cache)
    {
        _context = context;
        _plans = plans;
        _lists = lists;
        _cache = cache;
    }

    public async Task<GenerateShoppingListResult> Handle(GenerateShoppingListCommand request, CancellationToken cancellationToken)
    {
        var userId = _plans.RequireUserId();
        var plan = await _plans.LoadReadableAsync(request.PlanId, cancellationToken);
        if (plan.Kind == PlanKind.Template)
            throw new DomainValidationException("Apply the template first, then make the list from the plan.");

        var recipeIds = plan.Entries.Where(e => e.RecipeId.HasValue).Select(e => e.RecipeId!.Value).Distinct().ToList();
        var recipes = recipeIds.Count == 0
            ? new Dictionary<Guid, Recipe>()
            : await _context.Recipes.AsNoTracking().Include(r => r.Ingredients)
                .Where(r => recipeIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, cancellationToken);
        var foodIds = plan.Entries.Where(e => e.FoodId.HasValue).Select(e => e.FoodId!.Value)
            .Concat(recipes.Values.SelectMany(r => r.Ingredients).Where(i => i.FoodId.HasValue).Select(i => i.FoodId!.Value))
            .Distinct().ToList();
        var foods = foodIds.Count == 0
            ? new Dictionary<Guid, Food>()
            : await _context.Foods.AsNoTracking().Where(f => foodIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, cancellationToken);

        var draft = ShoppingAggregator.Aggregate(plan.Entries, recipes, foods);
        var now = DateTime.UtcNow;

        ShoppingList list;
        if (request.ReplaceListId is { } replaceId)
        {
            list = await _lists.LoadEditableAsync(replaceId, cancellationToken);
            _context.ShoppingListItems.RemoveRange(list.Items);
            list.Items.Clear();
            if (!string.IsNullOrWhiteSpace(request.Name)) list.Name = request.Name.Trim();
        }
        else
        {
            list = new ShoppingList
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                HouseholdId = plan.HouseholdId,
                Name = string.IsNullOrWhiteSpace(request.Name)
                    ? (string.IsNullOrWhiteSpace(plan.Name) ? "Shopping list" : $"{plan.Name} shopping list")
                    : request.Name.Trim(),
                CreatedAt = now,
            };
            _context.ShoppingLists.Add(list);
        }

        list.MealPlanId = plan.Id;
        list.GeneratedAt = now;
        list.UpdatedAt = now;

        var order = 0;
        foreach (var item in draft)
        {
            list.Items.Add(new ShoppingListItem
            {
                Id = Guid.NewGuid(),
                ShoppingListId = list.Id,
                FoodId = item.FoodId,
                ItemName = item.Name,
                Grams = item.Grams,
                Amount = item.Amount,
                Unit = item.Unit,
                NeedsReview = item.NeedsReview,
                PrepSessionId = item.PrepSessionId,
                SortOrder = order++,
                IsChecked = false,
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        await ShoppingListAccess.InvalidateAsync(_cache, list, cancellationToken);

        return new GenerateShoppingListResult(list.Id, draft.Count, draft.Count(i => i.NeedsReview));
    }
}
