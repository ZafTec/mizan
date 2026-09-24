using Mizan.Application.Exceptions;
using Mizan.Domain.Constants;
using Mizan.Domain.Entities;
using Mizan.Domain.Recipes;

namespace Mizan.Application.Common;

public static class DiaryEntryFactory
{
    public static FoodDiaryEntry FromFood(Food food, decimal grams, Guid userId, DateOnly date, string mealType, DateTime loggedAt)
    {
        if (grams <= 0 || food.ServingSize <= 0)
            throw new DomainValidationException($"Food '{food.Name}' needs a positive serving weight.");
        var scale = grams / 100m;
        return new FoodDiaryEntry
        {
            Id = Guid.NewGuid(), UserId = userId, FoodId = food.Id,
            EntryDate = date, MealType = MealTypes.Normalize(mealType),
            Name = food.Name, AmountGrams = grams, Servings = grams / food.ServingSize,
            Calories = Math.Round(food.CaloriesPer100g * scale, 2),
            ProteinGrams = Math.Round(food.ProteinPer100g * scale, 2),
            CarbsGrams = Math.Round(food.CarbsPer100g * scale, 2),
            FatGrams = Math.Round(food.FatPer100g * scale, 2),
            FiberGrams = food.FiberPer100g.HasValue ? Math.Round(food.FiberPer100g.Value * scale, 2) : null,
            ProteinCalorieRatio = Food.ComputeProteinCalorieRatio(food.CaloriesPer100g, food.ProteinPer100g),
            LoggedAt = loggedAt
        };
    }

    public static List<FoodDiaryEntry> FromRecipe(Recipe recipe, decimal servings, Guid userId, DateOnly date, string mealType, DateTime loggedAt)
    {
        // Unmeasured notes ("salt to taste") carry no nutrition; the recipe
        // page lists them as excluded, so skipping them here keeps the diary
        // equal to what was shown.
        var measured = recipe.Ingredients
            .Where(i => !RecipeNutritionCalculator.IsUnmeasuredNote(i))
            .OrderBy(i => i.SortOrder)
            .ToList();
        if (measured.Count == 0 || recipe.Servings <= 0)
            throw new DomainValidationException("This recipe needs measured ingredients and a serving count before it can be logged.");

        var unresolved = measured
            .Where(i => i.Food is null || !RecipeNutritionCalculator.Grams(i, i.Food).HasValue)
            .Select(i => i.IngredientText)
            .ToList();
        if (unresolved.Count > 0)
            throw new DomainValidationException(
                "Link a food and a weight in grams for: " + string.Join("; ", unresolved) + ". Then log the recipe again.");

        var entries = new List<FoodDiaryEntry>();
        var groupId = Guid.NewGuid();
        foreach (var ingredient in measured)
        {
            var grams = RecipeNutritionCalculator.Grams(ingredient, ingredient.Food!)!.Value;
            var entry = FromFood(ingredient.Food!, grams / recipe.Servings * servings, userId, date, mealType, loggedAt);
            entry.RecipeId = recipe.Id;
            entry.GroupId = groupId;
            entry.GroupName = recipe.Title;
            entries.Add(entry);
        }
        return entries;
    }
}
