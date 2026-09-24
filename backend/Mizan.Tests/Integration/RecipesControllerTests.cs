using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Mizan.Application.Common;
using Mizan.Application.Queries;
using Xunit;

namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public class RecipesControllerTests(ApiTestFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UserCanPromoteUpdateAndDeleteRecipe_ThroughBrowserOrMcp(bool service)
    {
        await fixture.ResetDatabaseAsync();
        var userId = Guid.NewGuid();
        var email = $"chef-{userId:N}@example.com";
        await fixture.SeedUserAsync(userId, email);
        var food = await fixture.SeedFoodAsync("Chicken Breast", 165m, 31m, 0m, 3.6m);
        using var client = Client(userId, email, service);
        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var i = 0; i < 2; i++)
        {
            var logged = await client.PostAsJsonAsync("/api/Nutrition/log", new { foodId = food.Id, entryDate = day, mealType = "LUNCH", servings = 1 });
            logged.EnsureSuccessStatusCode();
        }
        var promoted = await client.PostAsJsonAsync("/api/Recipes/promote", new { entryDate = day, mealType = "lunch", title = "Chicken Bowl" });
        promoted.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await promoted.Content.ReadFromJsonAsync<PromotionResponse>())!.RecipeId;

        var detail = await client.GetFromJsonAsync<RecipeDetailDto>($"/api/Recipes/{id}");
        detail!.Title.Should().Be("Chicken Bowl");
        detail.Nutrition!.CaloriesPerServing.Should().Be(330);
        detail.Ingredients.Should().HaveCount(2).And.OnlyContain(i => i.Unit == "g" && i.Amount == 100);

        var updated = await client.PutAsJsonAsync($"/api/Recipes/{id}", new
        {
            id, title = "Updated Bowl", servings = 3, isPublic = true, instructions = "Cook, then serve",
            ingredients = new[] { new { foodId = food.Id, ingredientText = "Chicken Breast", amount = 300, unit = "g" } }
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        var search = await client.GetFromJsonAsync<PagedResult<RecipeDto>>("/api/Recipes?searchTerm=updated");
        search!.Items.Should().Contain(r => r.Id == id);

        using var anonymous = fixture.CreateClient();
        (await anonymous.GetAsync($"/api/Recipes/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.DeleteAsync($"/api/Recipes/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/Recipes/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnmeasuredNotesAreExcludedFromNutritionAndLogging_WhileUnlinkedMeasuredLinesBlock()
    {
        await fixture.ResetDatabaseAsync();
        var userId = Guid.NewGuid();
        var email = $"import-{userId:N}@example.com";
        await fixture.SeedUserAsync(userId, email);
        var food = await fixture.SeedFoodAsync("Chicken Breast", 165m, 31m, 0m, 3.6m);
        using var client = fixture.CreateAuthenticatedClient(userId, email);
        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var i = 0; i < 2; i++)
            (await client.PostAsJsonAsync("/api/Nutrition/log", new { foodId = food.Id, entryDate = day, mealType = "LUNCH", servings = 1 })).EnsureSuccessStatusCode();
        var promoted = await client.PostAsJsonAsync("/api/Recipes/promote", new { entryDate = day, mealType = "lunch", title = "Imported Bowl" });
        promoted.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await promoted.Content.ReadFromJsonAsync<PromotionResponse>())!.RecipeId;

        (await client.PutAsJsonAsync($"/api/Recipes/{id}", new
        {
            id, title = "Imported Bowl", servings = 2, isPublic = false,
            ingredients = new object[]
            {
                new { foodId = food.Id, ingredientText = "400g chicken", amount = 400, unit = "g" },
                new { ingredientText = "salt, pepper, paprika" }
            }
        })).EnsureSuccessStatusCode();

        var detail = await client.GetFromJsonAsync<RecipeDetailDto>($"/api/Recipes/{id}");
        detail!.Nutrition!.CaloriesPerServing.Should().Be(330);
        detail.UnmeasuredIngredients.Should().Equal("salt, pepper, paprika");
        detail.UnresolvedIngredients.Should().BeEmpty();

        var logDay = day.AddDays(-1);
        (await client.PostAsJsonAsync("/api/Nutrition/log", new { recipeId = id, entryDate = logDay, mealType = "DINNER", servings = 1 }))
            .StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
        var daily = await client.GetFromJsonAsync<DailyNutritionResult>($"/api/Nutrition/daily?date={logDay:yyyy-MM-dd}");
        daily!.TotalCalories.Should().Be(detail.Nutrition.CaloriesPerServing);

        (await client.PutAsJsonAsync($"/api/Recipes/{id}", new
        {
            id, title = "Imported Bowl", servings = 2, isPublic = false,
            ingredients = new object[]
            {
                new { foodId = food.Id, ingredientText = "400g chicken", amount = 400, unit = "g" },
                new { ingredientText = "1 lemon (50g)", amount = 50, unit = "g" }
            }
        })).EnsureSuccessStatusCode();

        detail = await client.GetFromJsonAsync<RecipeDetailDto>($"/api/Recipes/{id}");
        detail!.Nutrition.Should().BeNull();
        detail.UnresolvedIngredients.Should().Equal("1 lemon (50g)");
        var refused = await client.PostAsJsonAsync("/api/Nutrition/log", new { recipeId = id, entryDate = logDay, mealType = "DINNER", servings = 1 });
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("1 lemon (50g)");
    }

    [Fact]
    public async Task StandaloneCreationReturnsGone_AndCannotBypassMealPromotion()
    {
        await fixture.ResetDatabaseAsync();
        var id = Guid.NewGuid();
        await fixture.SeedUserAsync(id, $"retired-{id}@example.com");
        using var client = fixture.CreateAuthenticatedClient(id, $"retired-{id}@example.com");
        var response = await client.PostAsJsonAsync("/api/Recipes", new { title = "Unlogged meal", servings = 1 });
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
        (await response.Content.ReadAsStringAsync()).Should().Contain("recipe_creation_requires_logged_meal");
        (await fixture.GetRecipesByUserId(id)).Should().BeEmpty();
    }

    [Fact]
    public async Task PrivateRecipeCannotBeReadFavoritedLoggedOrEditedByAnotherUser()
    {
        await fixture.ResetDatabaseAsync();
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        await fixture.SeedUserAsync(ownerId, $"owner-{ownerId}@example.com");
        await fixture.SeedUserAsync(otherId, $"other-{otherId}@example.com");
        var recipe = await fixture.SeedRecipeAsync(ownerId, "Private meal", "Mine", 1, 0);
        var food = await fixture.SeedFoodAsync("Rice", 130, 3, 28, 0);
        using var client = fixture.CreateAuthenticatedClient(otherId, $"other-{otherId}@example.com");

        (await client.GetAsync($"/api/Recipes/{recipe.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync($"/api/Recipes/{recipe.Id}/favorite", new { })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync("/api/Nutrition/log", new { recipeId = recipe.Id, entryDate = "2026-09-02" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetFromJsonAsync<PagedResult<RecipeDto>>("/api/Recipes?favoritesOnly=true"))!.Items.Should().BeEmpty();
        var updated = await client.PutAsJsonAsync($"/api/Recipes/{recipe.Id}", new
        {
            id = recipe.Id, title = "Hack", servings = 1,
            ingredients = new[] { new { foodId = food.Id, ingredientText = "Rice", amount = 100, unit = "g" } }
        });
        updated.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private HttpClient Client(Guid id, string email, bool service)
    {
        if (!service) return fixture.CreateAuthenticatedClient(id, email);
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
        client.DefaultRequestHeaders.Add("X-Impersonate-User", id.ToString());
        return client;
    }

    private sealed record PromotionResponse(Guid RecipeId);
}
