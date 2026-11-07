using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Contracts.Planning;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;

namespace Mizan.Tests.Integration;

/// <summary>
/// The people, foods and recipes the planning stories share, seeded straight
/// into the database, plus the request bodies they send. Everything the stories
/// assert goes through the API.
/// </summary>
internal sealed class PlanningWorld
{
    private readonly ApiTestFixture _fixture;

    public PlanningWorld(ApiTestFixture fixture) => _fixture = fixture;

    public async Task<(Guid Id, HttpClient Client)> PersonAsync(string label, string role = "user")
    {
        var id = Guid.NewGuid();
        var email = $"{label}-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email, role: role);
        return (id, _fixture.CreateAuthenticatedClient(id, email, role));
    }

    public Task<Food> FoodAsync(string name, decimal kcal, decimal protein, decimal carbs, decimal fat)
        => _fixture.SeedFoodAsync(name, kcal, protein, carbs, fat);

    /// <summary>A recipe with linked, weighed lines, or free lines when the food is null.</summary>
    public async Task<Recipe> RecipeAsync(Guid owner, string title, int servings, bool isPublic, params (Food? Food, decimal? Amount, string? Unit, string? Text)[] lines)
    {
        var recipe = await _fixture.SeedRecipeAsync(owner, title, "", servings, 10, isPublic);
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var order = 0;
        foreach (var line in lines)
        {
            db.RecipeIngredients.Add(new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                RecipeId = recipe.Id,
                FoodId = line.Food?.Id,
                Amount = line.Amount,
                Unit = line.Unit,
                IngredientText = line.Text ?? line.Food?.Name ?? "note",
                SortOrder = order++,
            });
        }
        await db.SaveChangesAsync();
        return recipe;
    }

    public async Task<Guid> HouseholdAsync(Guid owner, params (Guid UserId, bool CanEditShoppingList)[] members)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var household = new Household { Id = Guid.NewGuid(), Name = "Home", CreatedBy = owner, CreatedAt = DateTime.UtcNow };
        db.Households.Add(household);
        db.HouseholdMembers.Add(new HouseholdMember { HouseholdId = household.Id, UserId = owner, Role = "owner", JoinedAt = DateTime.UtcNow });
        foreach (var (userId, canEdit) in members)
            db.HouseholdMembers.Add(new HouseholdMember { HouseholdId = household.Id, UserId = userId, Role = "member", CanEditShoppingList = canEdit, JoinedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return household.Id;
    }

    public async Task CoachingAsync(Guid trainerId, Guid clientId, bool canPlanMeals)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        db.TrainerClientRelationships.Add(new TrainerClientRelationship
        {
            Id = Guid.NewGuid(),
            TrainerId = trainerId,
            ClientId = clientId,
            Status = "active",
            CanPlanMeals = canPlanMeals,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    public async Task<T> DbAsync<T>(Func<MizanDbContext, Task<T>> read)
    {
        using var scope = _fixture.Services.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<MizanDbContext>());
    }

    public static PlanEntryRequest RecipeEntry(Guid recipeId, decimal servings, int day, string slot = "DINNER", int? sessionIndex = null, Guid? sessionId = null) => new()
    {
        DayIndex = day,
        Slot = slot,
        Kind = "Recipe",
        RecipeId = recipeId,
        Servings = servings,
        PrepSessionIndex = sessionIndex,
        PrepSessionId = sessionId,
    };

    public static PlanEntryRequest FoodEntry(Guid foodId, decimal grams, int day, string slot = "LUNCH") => new()
    {
        DayIndex = day,
        Slot = slot,
        Kind = "Food",
        FoodId = foodId,
        Grams = grams,
    };

    public static PlanEntryRequest TextEntry(string text, int day, string slot = "SNACK") => new()
    {
        DayIndex = day,
        Slot = slot,
        Kind = "Text",
        Text = text,
    };

    public static async Task<Guid> CreatePlanAsync(HttpClient client, CreateMealPlanRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/MealPlans", request);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<MealPlanCreatedResult>())!.Id;
    }

    public static async Task<MealPlanDetailDto> PlanAsync(HttpClient client, Guid planId)
    {
        var response = await client.GetAsync($"/api/MealPlans/{planId}");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<MealPlanDetailDto>())!;
    }

    public static async Task<ShoppingListDetailDto> ListAsync(HttpClient client, Guid listId)
    {
        var response = await client.GetAsync($"/api/ShoppingLists/{listId}");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ShoppingListDetailDto>())!;
    }

    public static IEnumerable<PlanEntryDto> Entries(PlanDayDto day) => day.Slots.SelectMany(s => s.Entries);
}
