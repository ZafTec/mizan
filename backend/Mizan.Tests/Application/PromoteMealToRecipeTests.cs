using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Commands;
using Mizan.Application.Exceptions;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Mizan.Tests.Infrastructure;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>
/// Recipes are a byproduct of logging - see docs/REFOCUS.md §4. These pin the
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
        ingredients[0].Amount.Should().Be(1.5m);
        ingredients[1].IngredientText.Should().Be("Basmati rice");
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
    public async Task RefusesAMealContainingARecipe_UntilPreparationsExist()
    {
        var fromRecipe = Entry("Grandma's stew", minute: 0);
        fromRecipe.RecipeId = Guid.NewGuid();

        var (_, handler) = Make(fromRecipe, Entry("Bread", Guid.NewGuid(), minute: 1));

        var act = () => handler.Handle(
            new PromoteMealToRecipeCommand(Day, "dinner", "Stew and bread"),
            CancellationToken.None);

        // Emitting the stew as a text ingredient would produce a recipe with
        // quietly wrong macros. Refusing is the better failure until a recipe
        // can be marked as a preparation and carry a derived Food.
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*Grandma's stew*");
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
}
