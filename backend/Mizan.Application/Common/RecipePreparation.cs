using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Domain.Recipes;

namespace Mizan.Application.Common;

/// <summary>Derives a food without saving, so meal promotion can commit it atomically.</summary>
public static class RecipePreparation
{
    public static async Task<Food> DeriveAsync(
        IMizanDbContext context, Recipe recipe, Guid userId, decimal yieldGrams, CancellationToken ct)
    {
        if (yieldGrams <= 0)
            throw new DomainValidationException("A preparation needs a positive finished weight in grams.");
        var foodIds = recipe.Ingredients.Where(i => i.FoodId.HasValue).Select(i => i.FoodId!.Value).ToList();
        var foods = await context.Foods.Where(f => foodIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, ct);
        var totals = RecipeNutritionCalculator.Sum(recipe.Ingredients, foods, out var unresolved);
        if (recipe.Ingredients.Count == 0 || unresolved.Count > 0)
            throw new DomainValidationException("Ingredient nutrition is unknown: " + string.Join(", ", unresolved)
                + ". Every ingredient needs a linked food and a known weight.");

        var food = await context.Foods.FirstOrDefaultAsync(f => f.SourceRecipeId == recipe.Id && f.UserId == userId, ct);
        if (food is null)
        {
            food = new Food { Id = Guid.NewGuid(), UserId = userId, SourceRecipeId = recipe.Id, CreatedAt = DateTime.UtcNow };
            context.Foods.Add(food);
        }
        var per100g = totals.Per100g(yieldGrams);
        food.Name = recipe.Title;
        food.ServingSize = 100;
        food.ServingUnit = "g";
        food.CaloriesPer100g = per100g.Calories;
        food.ProteinPer100g = per100g.ProteinGrams;
        food.CarbsPer100g = per100g.CarbsGrams;
        food.FatPer100g = per100g.FatGrams;
        food.FiberPer100g = per100g.FiberGrams;
        food.ProteinCalorieRatio = per100g.ProteinCalorieRatio;
        food.IsVerified = false;
        food.UpdatedAt = DateTime.UtcNow;
        // A public recipe may be consumed by someone else. Its source remains
        // owned by its author; that viewer receives their own derived food.
        if (recipe.UserId == userId)
        {
            recipe.IsPreparation = true;
            recipe.YieldGrams = yieldGrams;
            recipe.UpdatedAt = DateTime.UtcNow;
        }
        return food;
    }
}
