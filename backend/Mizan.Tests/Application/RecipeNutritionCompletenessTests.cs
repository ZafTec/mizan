using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Domain.Entities;
using Mizan.Domain.Recipes;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>
/// Imported recipes often end with an unmeasured line such as "salt and pepper
/// to taste". It must not hide the nutrition of the measured ingredients or
/// block logging, while a measured line without a food still must.
/// </summary>
public class RecipeNutritionCompletenessTests
{
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly DateOnly Date = new(2026, 9, 24);

    // 100 kcal, 10 g protein per 100 g; 900 kcal, 100 g fat per 100 g.
    private static readonly Food Chicken = new() { Id = Guid.NewGuid(), Name = "Chicken", CaloriesPer100g = 100, ProteinPer100g = 10, ServingSize = 100 };
    private static readonly Food Oil = new() { Id = Guid.NewGuid(), Name = "Oil", CaloriesPer100g = 900, FatPer100g = 100, ServingSize = 100 };

    private static RecipeIngredient Measured(Food food, decimal grams, int order, string? unit = "g") => new()
    {
        Id = Guid.NewGuid(), FoodId = food.Id, Food = food, Amount = grams, Unit = unit,
        IngredientText = $"{grams}{unit} {food.Name}", SortOrder = order
    };

    private static RecipeIngredient Note(string text, int order) => new()
    {
        Id = Guid.NewGuid(), IngredientText = text, SortOrder = order
    };

    private static Recipe Recipe(int servings, params RecipeIngredient[] ingredients)
    {
        var recipe = new Recipe { Id = Guid.NewGuid(), Title = "Bowl", Servings = servings };
        foreach (var ingredient in ingredients)
        {
            ingredient.RecipeId = recipe.Id;
            recipe.Ingredients.Add(ingredient);
        }
        return recipe;
    }

    private static Dictionary<Guid, Food> Foods => new() { [Chicken.Id] = Chicken, [Oil.Id] = Oil };

    [Fact]
    public void Sum_SkipsUnmeasuredNote_AndReportsIt()
    {
        var recipe = Recipe(1, Measured(Chicken, 200, 0), Note("salt, pepper, paprika", 1));

        var totals = RecipeNutritionCalculator.Sum(recipe.Ingredients, Foods, out var unresolved, out var unmeasured);

        totals.Calories.Should().Be(200);
        unresolved.Should().BeEmpty();
        unmeasured.Should().Equal("salt, pepper, paprika");
    }

    [Fact]
    public void Sum_TreatsMeasuredLineWithoutFood_AsUnresolved()
    {
        var papaya = new RecipeIngredient { Id = Guid.NewGuid(), IngredientText = "600g papaya", Amount = 600, Unit = "g" };
        var recipe = Recipe(4, Measured(Chicken, 200, 0), papaya);

        RecipeNutritionCalculator.Sum(recipe.Ingredients, Foods, out var unresolved, out var unmeasured);

        unresolved.Should().Equal("600g papaya");
        unmeasured.Should().BeEmpty();
    }

    [Fact]
    public void FromRecipe_LogsMeasuredIngredients_AndMatchesDisplayedNutrition()
    {
        var recipe = Recipe(2, Measured(Chicken, 400, 0), Measured(Oil, 20, 1), Note("salt to taste", 2));
        var shown = RecipeNutritionCalculator.Sum(recipe.Ingredients, Foods, out _, out _).PerServing(recipe.Servings);

        var entries = DiaryEntryFactory.FromRecipe(recipe, 1.5m, UserId, Date, "lunch", DateTime.UtcNow);

        entries.Should().HaveCount(2);
        entries.Select(e => e.Name).Should().Equal("Chicken", "Oil");
        entries.Sum(e => e.Calories).Should().Be(shown.Calories * 1.5m);
        entries.Sum(e => e.FatGrams).Should().Be(shown.FatGrams * 1.5m);
        entries.Should().OnlyContain(e => e.RecipeId == recipe.Id && e.GroupId == entries[0].GroupId);
    }

    [Fact]
    public void FromRecipe_NamesEveryBlockingLine()
    {
        var papaya = new RecipeIngredient { Id = Guid.NewGuid(), IngredientText = "600g papaya", Amount = 600, Unit = "g", SortOrder = 1 };
        var oilByVolume = Measured(Oil, 15, 2, unit: "ml");
        var recipe = Recipe(1, Measured(Chicken, 100, 0), papaya, oilByVolume, Note("ice cubes", 3));

        var act = () => DiaryEntryFactory.FromRecipe(recipe, 1, UserId, Date, "lunch", DateTime.UtcNow);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*600g papaya*15ml Oil*")
            .Which.Message.Should().NotContain("ice cubes");
    }

    [Fact]
    public void FromRecipe_RefusesRecipeWithOnlyNotes()
    {
        var recipe = Recipe(1, Note("salt", 0));

        var act = () => DiaryEntryFactory.FromRecipe(recipe, 1, UserId, Date, "lunch", DateTime.UtcNow);

        act.Should().Throw<DomainValidationException>().WithMessage("*measured ingredients*");
    }

    [Fact]
    public async Task Lookup_ReportsNutrition_WithExcludedNotes_AndNoneWhenUnresolved()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var db = new MizanDbContext(options);
        var chicken = new Food { Id = Guid.NewGuid(), Name = "Chicken", CaloriesPer100g = 100, ProteinPer100g = 10 };
        db.Foods.Add(chicken);
        var complete = new Recipe { Id = Guid.NewGuid(), Title = "Complete", Servings = 2 };
        complete.Ingredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), FoodId = chicken.Id, Amount = 300, Unit = "g", IngredientText = "300g chicken", SortOrder = 0 });
        complete.Ingredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), IngredientText = "salt and pepper to taste", SortOrder = 1 });
        var blocked = new Recipe { Id = Guid.NewGuid(), Title = "Blocked", Servings = 1 };
        blocked.Ingredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), FoodId = chicken.Id, Amount = 100, Unit = "g", IngredientText = "100g chicken", SortOrder = 0 });
        blocked.Ingredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), Amount = 50, Unit = "g", IngredientText = "1 lemon (50g)", SortOrder = 1 });
        var notesOnly = new Recipe { Id = Guid.NewGuid(), Title = "Notes", Servings = 1 };
        notesOnly.Ingredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), IngredientText = "ice cubes", SortOrder = 0 });
        db.Recipes.AddRange(complete, blocked, notesOnly);
        await db.SaveChangesAsync();

        var statuses = await RecipeNutritionLookup.StatusesAsync(db, [complete.Id, blocked.Id, notesOnly.Id], CancellationToken.None);

        statuses[complete.Id].PerServing!.Value.Calories.Should().Be(150);
        statuses[complete.Id].Unmeasured.Should().Equal("salt and pepper to taste");
        statuses[blocked.Id].PerServing.Should().BeNull();
        statuses[blocked.Id].Unresolved.Should().Equal("1 lemon (50g)");
        statuses[notesOnly.Id].PerServing.Should().BeNull();
    }
}
