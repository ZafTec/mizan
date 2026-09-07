using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Mizan.Application.Commands;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Application.Queries;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Mizan.Tests.Infrastructure;
using Xunit;

namespace Mizan.Tests.Application;

public class LoggingRefocusTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateOnly _day = new(2026, 9, 2);
    private readonly MizanDbContext _db = new(new DbContextOptionsBuilder<MizanDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
    private readonly HybridCache _cache = new ServiceCollection().AddHybridCache().Services.BuildServiceProvider().GetRequiredService<HybridCache>();
    private readonly Mock<IStreakService> _streak = new();
    private readonly Mock<IAchievementEvaluator> _achievements = new();
    private FakeCurrentUser User => new() { UserId = _userId };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BothMealEndpointsRejectAnotherUsersPrivateFood(bool customEndpoint)
    {
        var food = new Food { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Name = "Private" };
        _db.Foods.Add(food);
        await _db.SaveChangesAsync();
        Func<Task> act = customEndpoint
            ? () => CustomHandler().Handle(new CreateFoodDiaryEntryCommand { FoodId = food.Id, EntryDate = _day }, default)
            : () => FoodHandler().Handle(new LogFoodCommand { FoodId = food.Id, EntryDate = _day }, default);

        await act.Should().ThrowAsync<EntityNotFoundException>();
        _db.FoodDiaryEntries.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BothMealEndpointsRejectAnotherUsersPrivateRecipe(bool customEndpoint)
    {
        var recipe = new Recipe { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Title = "Private" };
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync();
        Func<Task> act = customEndpoint
            ? () => CustomHandler().Handle(new CreateFoodDiaryEntryCommand { RecipeId = recipe.Id, EntryDate = _day }, default)
            : () => FoodHandler().Handle(new LogFoodCommand { RecipeId = recipe.Id, EntryDate = _day }, default);

        await act.Should().ThrowAsync<EntityNotFoundException>();
        _db.FoodDiaryEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task RecipeLoggingExpandsOneGroup_WithServingScaledSnapshots()
    {
        var oats = new Food { Id = Guid.NewGuid(), Name = "Oats", ServingSize = 40, CaloriesPer100g = 400, ProteinPer100g = 10, FiberPer100g = 8 };
        var milk = new Food { Id = Guid.NewGuid(), Name = "Milk", ServingSize = 200, CaloriesPer100g = 50, ProteinPer100g = 4 };
        _db.Foods.AddRange(oats, milk);
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(), UserId = _userId, Title = "Porridge", Servings = 2,
            Ingredients =
            {
                new RecipeIngredient { Id = Guid.NewGuid(), FoodId = oats.Id, IngredientText = "Oats", Amount = 80, Unit = "g", SortOrder = 0 },
                new RecipeIngredient { Id = Guid.NewGuid(), FoodId = milk.Id, IngredientText = "Milk", Amount = 400, Unit = "g", SortOrder = 1 }
            }
        };
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync();

        var result = await FoodHandler().Handle(new LogFoodCommand { RecipeId = recipe.Id, EntryDate = _day, Servings = 1.5m }, default);

        result.Calories.Should().Be(390);
        result.ProteinGrams.Should().Be(18);
        var entries = await _db.FoodDiaryEntries.ToListAsync();
        entries.Should().HaveCount(2);
        entries.Select(e => e.GroupId).Distinct().Should().ContainSingle().Which.Should().NotBeNull();
        entries.Should().OnlyContain(e => e.GroupName == "Porridge" && e.RecipeId == recipe.Id);
        entries.Single(e => e.FoodId == oats.Id).AmountGrams.Should().Be(60);
        entries.Single(e => e.FoodId == oats.Id).FiberGrams.Should().Be(4.8m);

        oats.CaloriesPer100g = 999;
        await _db.SaveChangesAsync();
        (await new GetFoodDiaryQueryHandler(_db, User).Handle(new GetFoodDiaryQuery { Date = _day }, default))
            .Totals.Calories.Should().Be(390);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecipeWithAnUnweighedIngredientDoesNotPartiallyLog(bool customEndpoint)
    {
        var food = new Food { Id = Guid.NewGuid(), Name = "Milk" };
        _db.Foods.Add(food);
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(), UserId = _userId, Title = "Unknown quantity",
            Ingredients = { new RecipeIngredient { Id = Guid.NewGuid(), FoodId = food.Id, IngredientText = "A cup of milk", Amount = 1, Unit = "cup" } }
        };
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync();

        Func<Task> act = customEndpoint
            ? () => CustomHandler().Handle(new CreateFoodDiaryEntryCommand { RecipeId = recipe.Id, EntryDate = _day }, default)
            : () => FoodHandler().Handle(new LogFoodCommand { RecipeId = recipe.Id, EntryDate = _day }, default);
        await act.Should().ThrowAsync<DomainValidationException>();
        _db.FoodDiaryEntries.Should().BeEmpty();
        var detail = await new GetRecipeByIdQueryHandler(_db, User, _cache).Handle(new GetRecipeByIdQuery(recipe.Id), default);
        detail!.Nutrition.Should().BeNull();
        var list = await new GetRecipesQueryHandler(_db, User, _cache).Handle(new GetRecipesQuery(), default);
        list.Items.Single(r => r.Id == recipe.Id).Nutrition.Should().BeNull();
    }

    [Fact]
    public async Task CustomEndpointExpandsRecipesAndPreservesBackfilledDate()
    {
        var food = new Food { Id = Guid.NewGuid(), Name = "Oats", CaloriesPer100g = 400 };
        _db.Foods.Add(food);
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(), UserId = _userId, Title = "Porridge", Servings = 2,
            Ingredients = { new RecipeIngredient { Id = Guid.NewGuid(), FoodId = food.Id, IngredientText = "Oats", Amount = 80, Unit = "g" } }
        };
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync();
        var loggedAt = new DateTime(2026, 9, 2, 9, 0, 0, DateTimeKind.Utc);

        await CustomHandler().Handle(new CreateFoodDiaryEntryCommand
        {
            RecipeId = recipe.Id, EntryDate = _day, LoggedAt = loggedAt, Servings = 1.5m, AmountGrams = 123
        }, default);

        var entry = await _db.FoodDiaryEntries.SingleAsync();
        entry.FoodId.Should().Be(food.Id);
        entry.GroupId.Should().NotBeNull();
        entry.AmountGrams.Should().Be(60);
        entry.Calories.Should().Be(240);
        entry.LoggedAt.Should().Be(loggedAt);
    }

    [Fact]
    public async Task BackfilledTimestampUsesTheUsersDay_ForTheEntryAndStreak()
    {
        var clock = new Mock<IUserClock>();
        clock.Setup(c => c.TimeZoneIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync("America/Los_Angeles");
        var handler = new CreateFoodDiaryEntryCommandHandler(_db, User, _streak.Object, _achievements.Object, _cache, clock.Object);

        await handler.Handle(new CreateFoodDiaryEntryCommand
        {
            Name = "Late dinner", Calories = 300, LoggedAt = new DateTime(2026, 9, 3, 2, 0, 0, DateTimeKind.Utc)
        }, default);

        (await _db.FoodDiaryEntries.SingleAsync()).EntryDate.Should().Be(_day);
        _streak.Verify(s => s.RecordActivityAsync("nutrition", _day, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CustomEndpointResolvesReferencedFoodNutrition_WhenMacrosAreOmitted()
    {
        var food = new Food { Id = Guid.NewGuid(), Name = "Oats", CaloriesPer100g = 400, ProteinPer100g = 10, FiberPer100g = 8, ServingSize = 40 };
        _db.Foods.Add(food);
        await _db.SaveChangesAsync();

        await CustomHandler().Handle(new CreateFoodDiaryEntryCommand { FoodId = food.Id, EntryDate = _day, Servings = 2, AmountGrams = 123 }, default);

        var entry = await _db.FoodDiaryEntries.SingleAsync();
        entry.Calories.Should().Be(320);
        entry.FiberGrams.Should().Be(6.4m);
        entry.AmountGrams.Should().Be(80);
        entry.Name.Should().Be("Oats");
    }

    [Fact]
    public async Task CustomMealPreservesConfirmedWeight_ForDiaryAndPromotion()
    {
        var request = new CreateFoodDiaryEntryCommand
        {
            Name = "Photo meal", EntryDate = _day, Servings = 2,
            AmountGrams = 175, Calories = 300, ProteinGrams = 20, CarbsGrams = 30, FatGrams = 8
        };
        new CreateFoodDiaryEntryCommandValidator().Validate(request).IsValid.Should().BeTrue();

        await CustomHandler().Handle(request, default);

        var entry = await _db.FoodDiaryEntries.SingleAsync();
        entry.AmountGrams.Should().Be(175);
        entry.Servings.Should().Be(2);
        entry.Calories.Should().Be(300);
        entry.ProteinGrams.Should().Be(20);
        var diary = await new GetFoodDiaryQueryHandler(_db, User).Handle(new GetFoodDiaryQuery { Date = _day }, default);
        diary.Entries.Single().AmountGrams.Should().Be(175);

        await CustomHandler().Handle(request with
        {
            Name = "Bread", Servings = 1, AmountGrams = 50,
            Calories = 100, ProteinGrams = 3, CarbsGrams = 17, FatGrams = 1
        }, default);
        var recipeId = await new PromoteMealToRecipeCommandHandler(_db, User, _cache).Handle(
            new PromoteMealToRecipeCommand(_day, request.MealType, "Photo meal and bread"), default);
        var recipe = await _db.Recipes.Include(r => r.Ingredients).SingleAsync(r => r.Id == recipeId);
        recipe.Ingredients.Single(i => i.IngredientText == "Photo meal").Amount.Should().Be(175);
    }

    private LogFoodCommandHandler FoodHandler() => new(_db, User, _streak.Object, _achievements.Object, _cache);
    private CreateFoodDiaryEntryCommandHandler CustomHandler() => new(_db, User, _streak.Object, _achievements.Object, _cache, Mock.Of<IUserClock>());
}
