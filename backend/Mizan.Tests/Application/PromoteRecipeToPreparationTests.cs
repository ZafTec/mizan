using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Commands;
using Mizan.Application.Exceptions;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Mizan.Tests.Infrastructure;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>
/// Preparations - see docs/REFOCUS.md §4. Marking a recipe as a preparation
/// derives a Food, which is how homemade mayonnaise gets reused with correct
/// macros without recipes referencing each other.
/// </summary>
public class PromoteRecipeToPreparationTests
{
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid RecipeId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid OilId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid EggId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private static (MizanDbContext db, PromoteRecipeToPreparationCommandHandler handler) Make(
        decimal? yieldGrams = null,
        Guid? recipeOwner = null,
        bool linkIngredients = true)
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new MizanDbContext(options);

        // 100 g oil at 900 kcal, 50 g egg at 140 kcal / 12 g protein per 100 g.
        db.Foods.AddRange(
            new Food { Id = OilId, Name = "Oil", CaloriesPer100g = 900, FatPer100g = 100 },
            new Food { Id = EggId, Name = "Egg", CaloriesPer100g = 140, ProteinPer100g = 12 });

        db.Recipes.Add(new Recipe
        {
            Id = RecipeId,
            UserId = recipeOwner ?? UserId,
            Title = "Low-fat mayonnaise",
            YieldGrams = yieldGrams,
            Ingredients =
            {
                new RecipeIngredient { Id = Guid.NewGuid(), RecipeId = RecipeId, FoodId = linkIngredients ? OilId : null, IngredientText = "Oil", Amount = 100 },
                new RecipeIngredient { Id = Guid.NewGuid(), RecipeId = RecipeId, FoodId = EggId, IngredientText = "Egg", Amount = 50 }
            }
        });
        db.SaveChanges();

        return (db, new PromoteRecipeToPreparationCommandHandler(db, new FakeCurrentUser { UserId = UserId }));
    }

    [Fact]
    public async Task DerivesPer100gMacrosFromIngredientsAndYield()
    {
        // 900 + 70 = 970 kcal over a 150 g yield => 646.67 kcal / 100 g
        var (db, handler) = Make(yieldGrams: 150);

        var foodId = await handler.Handle(
            new PromoteRecipeToPreparationCommand(RecipeId), CancellationToken.None);

        var food = await db.Foods.SingleAsync(f => f.Id == foodId);
        food.Name.Should().Be("Low-fat mayonnaise");
        food.UserId.Should().Be(UserId, "a derived food is private to its owner");
        food.SourceRecipeId.Should().Be(RecipeId);
        food.CaloriesPer100g.Should().BeApproximately(646.67m, 0.01m);
        food.ProteinPer100g.Should().BeApproximately(4m, 0.01m);
        food.IsVerified.Should().BeFalse();

        var recipe = await db.Recipes.SingleAsync();
        recipe.IsPreparation.Should().BeTrue();
        recipe.YieldGrams.Should().Be(150);
    }

    [Fact]
    public async Task YieldMayBeSuppliedOnTheCall()
    {
        var (db, handler) = Make(yieldGrams: null);

        await handler.Handle(
            new PromoteRecipeToPreparationCommand(RecipeId, YieldGrams: 200), CancellationToken.None);

        (await db.Recipes.SingleAsync()).YieldGrams.Should().Be(200);
    }

    [Fact]
    public async Task RefusesWithoutAYield_BecauseServingsCannotBecomePer100g()
    {
        var (_, handler) = Make(yieldGrams: null);

        var act = () => handler.Handle(
            new PromoteRecipeToPreparationCommand(RecipeId), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*finished weight*");
    }

    [Fact]
    public async Task RefusesWhenAnIngredientHasNoLinkedFood()
    {
        var (_, handler) = Make(yieldGrams: 150, linkIngredients: false);

        var act = () => handler.Handle(
            new PromoteRecipeToPreparationCommand(RecipeId), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*nutrition is unknown*");
    }

    [Fact]
    public async Task RePromotingUpdatesTheSameFood_RatherThanDuplicatingIt()
    {
        var (db, handler) = Make(yieldGrams: 150);

        var first = await handler.Handle(
            new PromoteRecipeToPreparationCommand(RecipeId), CancellationToken.None);
        var second = await handler.Handle(
            new PromoteRecipeToPreparationCommand(RecipeId, YieldGrams: 300), CancellationToken.None);

        second.Should().Be(first);
        db.Foods.Count(f => f.SourceRecipeId == RecipeId).Should().Be(1);

        var food = await db.Foods.SingleAsync(f => f.Id == first);
        food.CaloriesPer100g.Should().BeApproximately(323.33m, 0.01m, "doubling the yield halves the density");
    }

    [Fact]
    public async Task RefusesAnotherUsersRecipe()
    {
        var (_, handler) = Make(yieldGrams: 150, recipeOwner: Guid.NewGuid());

        var act = () => handler.Handle(
            new PromoteRecipeToPreparationCommand(RecipeId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }
}
