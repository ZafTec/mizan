using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Commands;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Mizan.Tests.Infrastructure;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>
/// Recipes are a byproduct of logging - see docs/ARCHITECTURE.md#navigation-and-logging. These pin the
/// promotion path, which is the only way a recipe gets authored.
/// </summary>
public class PromoteMealToRecipeTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateOnly Day = new(2026, 8, 21);

    private static (MizanDbContext db, PromoteMealToRecipeCommandHandler handler) Make(
        params FoodDiaryEntry[] entries)
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new MizanDbContext(options);
        foreach (var entry in entries.Where(e => e.FoodId.HasValue).DistinctBy(e => e.FoodId))
            db.Foods.Add(new Food { Id = entry.FoodId!.Value, Name = entry.Name, ServingSize = 100, CaloriesPer100g = 100 });
        db.FoodDiaryEntries.AddRange(entries);
        db.SaveChanges();

        var currentUser = new FakeCurrentUser { UserId = UserId };
        var cache = new ServiceCollection().AddHybridCache().Services
            .BuildServiceProvider().GetRequiredService<HybridCache>();
        return (db, new PromoteMealToRecipeCommandHandler(db, currentUser, cache));
    }

    private static FoodDiaryEntry Entry(string name, Guid? foodId = null, string mealType = "dinner", int minute = 0)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            FoodId = foodId,
            EntryDate = Day,
            MealType = mealType,
            Name = name,
            Servings = 1.5m,
            LoggedAt = new DateTime(2026, 8, 21, 19, minute, 0, DateTimeKind.Utc)
        };

    [Fact]
    public async Task PromotesTheLoggedMeal_KeepingOrderAndQuantities()
    {
        var chickenId = Guid.NewGuid();
        var (db, handler) = Make(
            Entry("Chicken breast", chickenId, minute: 0),
            Entry("Basmati rice", Guid.NewGuid(), minute: 5));

        var recipeId = await handler.Handle(
            new PromoteMealToRecipeCommand(Day, "dinner", "Chicken and rice"),
            CancellationToken.None);

        var recipe = await db.Recipes.Include(r => r.Ingredients).SingleAsync();
        recipe.Id.Should().Be(recipeId);
        recipe.Title.Should().Be("Chicken and rice");
        recipe.UserId.Should().Be(UserId);
        recipe.IsPublic.Should().BeFalse();

        var ingredients = recipe.Ingredients.OrderBy(i => i.SortOrder).ToList();
        ingredients.Should().HaveCount(2);
        ingredients[0].IngredientText.Should().Be("Chicken breast");
        ingredients[0].FoodId.Should().Be(chickenId);
        ingredients[0].Amount.Should().Be(150m);
        ingredients[0].Unit.Should().Be("g");
        ingredients[1].IngredientText.Should().Be("Basmati rice");
        (await RecipeNutritionLookup.ForRecipeAsync(db, recipeId, CancellationToken.None)).Calories.Should().Be(300);
    }

    [Fact]
    public async Task RefusesASingleItem_BecauseThatIsJustTheFood()
    {
        var (_, handler) = Make(Entry("Banana", Guid.NewGuid()));

        var act = () => handler.Handle(
            new PromoteMealToRecipeCommand(Day, "dinner", "Banana"),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*at least 2*");
    }

    [Fact]
    public async Task OnlyTakesTheNamedMeal_NotTheWholeDay()
    {
        var (db, handler) = Make(
            Entry("Oats", Guid.NewGuid(), mealType: "breakfast", minute: 0),
            Entry("Milk", Guid.NewGuid(), mealType: "breakfast", minute: 1),
            Entry("Steak", Guid.NewGuid(), mealType: "dinner", minute: 2),
            Entry("Potatoes", Guid.NewGuid(), mealType: "dinner", minute: 3));

        await handler.Handle(
            new PromoteMealToRecipeCommand(Day, "dinner", "Steak dinner"),
            CancellationToken.None);

        var recipe = await db.Recipes.Include(r => r.Ingredients).SingleAsync();
        recipe.Ingredients.Select(i => i.IngredientText)
            .Should().BeEquivalentTo(new[] { "Steak", "Potatoes" });
    }

    [Fact]
    public async Task RequestsMissingRecipeWeight_WithoutSavingPartialPreparations()
    {
        var fromRecipe = Entry("Grandma's stew", minute: 0);
        fromRecipe.RecipeId = Guid.NewGuid();

        var (db, handler) = Make(fromRecipe, Entry("Bread", Guid.NewGuid(), minute: 1));
        db.Recipes.Add(new Recipe { Id = fromRecipe.RecipeId.Value, UserId = UserId, Title = "Grandma's stew" });
        await db.SaveChangesAsync();

        var act = () => handler.Handle(
            new PromoteMealToRecipeCommand(Day, "dinner", "Stew and bread"),
            CancellationToken.None);

        var error = await act.Should().ThrowAsync<PromotionWeightsRequiredException>();
        error.Which.Items.Should().ContainSingle(i => i.Id == fromRecipe.RecipeId && i.Kind == "recipe");
        db.Recipes.Should().HaveCount(1);
        db.Foods.Should().HaveCount(1);
    }

    [Fact]
    public async Task IgnoresAnotherUsersEntries()
    {
        var mine = Entry("Rice", Guid.NewGuid(), minute: 0);
        var theirs = Entry("Rice", Guid.NewGuid(), minute: 1);
        theirs.UserId = Guid.NewGuid();

        var (_, handler) = Make(mine, theirs);

        var act = () => handler.Handle(
            new PromoteMealToRecipeCommand(Day, "dinner", "Not mine"),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task MixedRecipeMealDerivesPreparation_AndPreservesConsumedAmount()
    {
        var fromRecipe = Entry("Stew");
        fromRecipe.RecipeId = Guid.NewGuid();
        var bread = Entry("Bread", Guid.NewGuid(), minute: 1);
        var (db, handler) = Make(fromRecipe, bread);
        var ingredientFood = new Food { Id = Guid.NewGuid(), Name = "Beans", CaloriesPer100g = 120, ProteinPer100g = 10 };
        db.Foods.Add(ingredientFood);
        db.Recipes.Add(new Recipe
        {
            Id = fromRecipe.RecipeId.Value, UserId = UserId, Title = "Stew", Servings = 2, YieldGrams = 400,
            Ingredients = { new RecipeIngredient { Id = Guid.NewGuid(), FoodId = ingredientFood.Id, IngredientText = "Beans", Amount = 200, Unit = "g" } }
        });
        await db.SaveChangesAsync();

        var id = await handler.Handle(new PromoteMealToRecipeCommand(Day, "DINNER", "Stew and bread"), CancellationToken.None);

        var recipe = await db.Recipes.Include(r => r.Ingredients).SingleAsync(r => r.Id == id);
        var preparation = await db.Foods.SingleAsync(f => f.SourceRecipeId == fromRecipe.RecipeId);
        recipe.Ingredients.Single(i => i.FoodId == preparation.Id).Amount.Should().Be(300);
        (await RecipeNutritionLookup.ForRecipeAsync(db, id, CancellationToken.None)).Calories.Should().Be(330);
        (await db.Recipes.SingleAsync(r => r.Id == fromRecipe.RecipeId)).IsPreparation.Should().BeTrue();
    }

    [Fact]
    public async Task PromotesManualNutrition_OnlyAfterItsWeightIsKnown()
    {
        var manual = Entry("Soup");
        manual.Calories = 90; manual.ProteinGrams = 4; manual.CarbsGrams = 10; manual.FatGrams = 3;
        var (db, handler) = Make(manual, Entry("Bread", Guid.NewGuid(), minute: 1));
        var command = new PromoteMealToRecipeCommand(Day, "dinner", "Soup and bread");

        var error = await FluentActions.Awaiting(() => handler.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<PromotionWeightsRequiredException>();
        error.Which.Items.Should().ContainSingle(i => i.Id == manual.Id && i.Kind == "entry");
        var id = await handler.Handle(command with { EntryWeightsGrams = new() { [manual.Id] = 300 } }, CancellationToken.None);

        var totals = await RecipeNutritionLookup.ForRecipeAsync(db, id, CancellationToken.None);
        totals.Calories.Should().Be(240);
        totals.ProteinGrams.Should().BeApproximately(4, 0.02m);
        db.Foods.Should().Contain(f => f.UserId == UserId && f.Name == "Soup");
    }

    [Fact]
    public async Task PromotionKeepsLoggedMacros_WhenTheFoodHasChanged()
    {
        var oats = Entry("Oats", Guid.NewGuid());
        oats.Calories = 200; oats.ProteinGrams = 20; oats.CarbsGrams = 30; oats.FatGrams = 2;
        oats.AmountGrams = 50;
        var (db, handler) = Make(oats, Entry("Milk", Guid.NewGuid(), minute: 1));

        var id = await handler.Handle(new PromoteMealToRecipeCommand(Day, "dinner", "Oats and milk"), CancellationToken.None);

        var totals = await RecipeNutritionLookup.ForRecipeAsync(db, id, CancellationToken.None);
        totals.Calories.Should().Be(350);
        totals.ProteinGrams.Should().Be(20);
        db.Foods.Should().Contain(f => f.UserId == UserId && f.Name == "Oats" && f.CaloriesPer100g == 400);
    }

    [Fact]
    public async Task CannotPromoteIntoAnUnrelatedHousehold()
    {
        var (db, handler) = Make(Entry("Rice", Guid.NewGuid()), Entry("Chicken", Guid.NewGuid(), minute: 1));
        await FluentActions.Awaiting(() => handler.Handle(
            new PromoteMealToRecipeCommand(Day, "dinner", "Dinner", Guid.NewGuid()), CancellationToken.None))
            .Should().ThrowAsync<ForbiddenAccessException>();
        db.Recipes.Should().BeEmpty();
    }
}
