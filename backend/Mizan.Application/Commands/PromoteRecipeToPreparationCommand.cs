using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

/// <summary>
/// Marks a recipe as a preparation and derives a private <see cref="Food"/> from
/// it, so it can be used as an ingredient in other recipes and logged on its own.
/// See docs/REFOCUS.md §4.
///
/// Macros are SNAPSHOTTED here rather than computed on read. That is what makes
/// cycles impossible rather than merely detectable: nothing recurses at query
/// time. Editing the recipe and re-promoting refreshes the derived food, and
/// entries already logged keep the macros they were logged with.
/// </summary>
public record PromoteRecipeToPreparationCommand(
    Guid RecipeId,
    decimal? YieldGrams = null
) : IRequest<Guid>;

public class PromoteRecipeToPreparationCommandValidator : AbstractValidator<PromoteRecipeToPreparationCommand>
{
    public PromoteRecipeToPreparationCommandValidator()
    {
        RuleFor(x => x.RecipeId).NotEmpty();
        RuleFor(x => x.YieldGrams)
            .GreaterThan(0).When(x => x.YieldGrams.HasValue)
            .WithMessage("Yield must be greater than zero grams");
    }
}

public class PromoteRecipeToPreparationCommandHandler
    : IRequestHandler<PromoteRecipeToPreparationCommand, Guid>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public PromoteRecipeToPreparationCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(PromoteRecipeToPreparationCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var userId = _currentUser.UserId.Value;

        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId, cancellationToken)
            ?? throw new EntityNotFoundException("Recipe", request.RecipeId);

        if (recipe.UserId != userId)
        {
            throw new ForbiddenAccessException("Recipe does not belong to the current user");
        }

        var yieldGrams = request.YieldGrams ?? recipe.YieldGrams;
        if (yieldGrams is null or <= 0)
        {
            throw new DomainValidationException(
                "This recipe needs a finished weight in grams before it can be used as an ingredient. "
                + "Per-100g nutrition cannot be derived from a serving count.");
        }

        var totals = await SumIngredientsAsync(recipe, cancellationToken);
        var factor = 100m / yieldGrams.Value;

        var food = await _context.Foods
            .FirstOrDefaultAsync(f => f.SourceRecipeId == recipe.Id && f.UserId == userId, cancellationToken);

        var isNew = food is null;
        food ??= new Food
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SourceRecipeId = recipe.Id,
            CreatedAt = DateTime.UtcNow
        };

        food.Name = recipe.Title;
        food.ServingSize = 100;
        food.ServingUnit = "g";
        food.CaloriesPer100g = Round(totals.Calories * factor);
        food.ProteinPer100g = Round(totals.Protein * factor);
        food.CarbsPer100g = Round(totals.Carbs * factor);
        food.FatPer100g = Round(totals.Fat * factor);
        food.FiberPer100g = Round(totals.Fiber * factor);
        food.ProteinCalorieRatio = Food.ComputeProteinCalorieRatio(food.CaloriesPer100g, food.ProteinPer100g);
        food.IsVerified = false;
        food.UpdatedAt = DateTime.UtcNow;

        if (isNew)
        {
            _context.Foods.Add(food);
        }

        recipe.IsPreparation = true;
        recipe.YieldGrams = yieldGrams;
        recipe.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return food.Id;
    }

    private async Task<Totals> SumIngredientsAsync(Recipe recipe, CancellationToken cancellationToken)
    {
        var foodIds = recipe.Ingredients.Where(i => i.FoodId.HasValue).Select(i => i.FoodId!.Value).ToList();
        var foods = await _context.Foods
            .Where(f => foodIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, cancellationToken);

        var totals = new Totals();
        foreach (var ingredient in recipe.Ingredients)
        {
            if (!ingredient.FoodId.HasValue || !foods.TryGetValue(ingredient.FoodId.Value, out var food))
            {
                throw new DomainValidationException(
                    $"Ingredient '{ingredient.IngredientText}' has no linked food, so its nutrition is unknown. "
                    + "Every ingredient must resolve to a food before this recipe can become one.");
            }

            // Amount is in grams for derivation purposes; per-100g values scale by /100.
            var grams = ingredient.Amount ?? 0m;
            var scale = grams / 100m;

            totals.Calories += food.CaloriesPer100g * scale;
            totals.Protein += food.ProteinPer100g * scale;
            totals.Carbs += food.CarbsPer100g * scale;
            totals.Fat += food.FatPer100g * scale;
            totals.Fiber += (food.FiberPer100g ?? 0m) * scale;
        }

        return totals;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2);

    private sealed class Totals
    {
        public decimal Calories;
        public decimal Protein;
        public decimal Carbs;
        public decimal Fat;
        public decimal Fiber;
    }
}
