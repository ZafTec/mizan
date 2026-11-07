using Mizan.Domain.Entities;
using Mizan.Domain.Planning;

namespace Mizan.Tests.PlanningRules;

/// <summary>Small builders so the planning tables read as tables.</summary>
internal static class PlanFixtures
{
    public static Food Food(string name, decimal kcal = 100, decimal protein = 10, decimal carbs = 10, decimal fat = 2, decimal servingSize = 100) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        CaloriesPer100g = kcal,
        ProteinPer100g = protein,
        CarbsPer100g = carbs,
        FatPer100g = fat,
        FiberPer100g = 1,
        ServingSize = servingSize,
    };

    public static Recipe Recipe(string title, int servings, params RecipeIngredient[] lines)
    {
        var recipe = new Recipe { Id = Guid.NewGuid(), Title = title, Servings = servings };
        var order = 0;
        foreach (var line in lines)
        {
            line.RecipeId = recipe.Id;
            line.SortOrder = order++;
            recipe.Ingredients.Add(line);
        }
        return recipe;
    }

    public static RecipeIngredient Line(Food? food, decimal? amount, string? unit, string? text = null) => new()
    {
        Id = Guid.NewGuid(),
        FoodId = food?.Id,
        Food = food,
        Amount = amount,
        Unit = unit,
        IngredientText = text ?? food?.Name ?? "note",
    };

    public static PlanEntry RecipeEntry(Recipe recipe, decimal servings, int day = 0, string slot = "DINNER", Guid? session = null) => new()
    {
        Id = Guid.NewGuid(),
        DayIndex = day,
        Slot = slot,
        Kind = PlanEntryKind.Recipe,
        RecipeId = recipe.Id,
        Servings = servings,
        PrepSessionId = session,
    };

    public static PlanEntry FoodEntry(Food food, decimal grams, int day = 0, string slot = "LUNCH", Guid? session = null) => new()
    {
        Id = Guid.NewGuid(),
        DayIndex = day,
        Slot = slot,
        Kind = PlanEntryKind.Food,
        FoodId = food.Id,
        Grams = grams,
        PrepSessionId = session,
    };

    public static PlanEntry TextEntry(string text, int day = 0, string slot = "SNACK", Guid? session = null) => new()
    {
        Id = Guid.NewGuid(),
        DayIndex = day,
        Slot = slot,
        Kind = PlanEntryKind.Text,
        Text = text,
        PrepSessionId = session,
    };

    public static Dictionary<Guid, Food> Foods(params Food[] foods) => foods.ToDictionary(f => f.Id);

    public static Dictionary<Guid, Recipe> Recipes(params Recipe[] recipes) => recipes.ToDictionary(r => r.Id);
}
