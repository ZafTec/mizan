using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Mizan.Application.Common;
using Mizan.Application.Queries;
using Xunit;

namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public class RecipesControllerTests
{
    private readonly ApiTestFixture _fixture;

    public RecipesControllerTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UserCanCreateUpdateDeleteRecipe()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"chef-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);

        var food = await _fixture.SeedFoodAsync("Chicken Breast", 165m, 31m, 0m, 3.6m);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var createCommand = new
        {
            Title = "Chicken Bowl",
            Description = "Simple recipe",
            Servings = 2,
            PrepTimeMinutes = 10,
            CookTimeMinutes = 15,
            IsPublic = true,
            Ingredients = new[]
            {
                new
                {
                    FoodId = food.Id,
                    IngredientText = "Chicken Breast",
                    Amount = 200m,
                    Unit = "g"
                }
            },
            Instructions = new[] { "Cook chicken", "Serve" },
            Tags = new[] { "protein" }
        };

        var createResponse = await client.PostAsJsonAsync("/api/Recipes", createCommand);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateRecipeResponse>();
        created.Should().NotBeNull();
        created!.Id.Should().NotBeEmpty();

        var getResponse = await client.GetAsync($"/api/Recipes/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await getResponse.Content.ReadFromJsonAsync<RecipeDetailResponse>();
        fetched.Should().NotBeNull();
        fetched!.Title.Should().Be("Chicken Bowl");
        fetched.Tags.Should().Contain("protein");

        var updateCommand = new
        {
            Id = created.Id,
            Title = "Updated Bowl",
            Description = "Updated",
            Servings = 3,
            PrepTimeMinutes = 10,
            CookTimeMinutes = 15,
            IsPublic = true,
            Ingredients = new[]
            {
                new
                {
                    FoodId = food.Id,
                    IngredientText = "Chicken Breast",
                    Amount = 300m,
                    Unit = "g"
                }
            },
            Instructions = new[] { "Cook", "Serve" },
            Tags = new[] { "updated" }
        };

        var updateResponse = await client.PutAsJsonAsync($"/api/Recipes/{created.Id}", updateCommand);
        
        if (updateResponse.StatusCode != HttpStatusCode.OK)
        {
            var error = await updateResponse.Content.ReadAsStringAsync();
            throw new Exception($"Update Failed: {updateResponse.StatusCode} - {error}");
        }

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var searchResponse = await client.GetAsync("/api/Recipes?searchTerm=updated");
        searchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var searchResult = await searchResponse.Content.ReadFromJsonAsync<PagedResult<RecipeDto>>();
        searchResult.Should().NotBeNull();
        searchResult!.Items.Should().Contain(r => r.Id == created.Id);

        var deleteResponse = await client.DeleteAsync($"/api/Recipes/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getDeleted = await client.GetAsync($"/api/Recipes/{created.Id}");
        getDeleted.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonOwnerCannotUpdatePrivateRecipe()
    {
        await _fixture.ResetDatabaseAsync();

        var ownerId = Guid.NewGuid();
        var ownerEmail = $"owner-{ownerId:N}@example.com";
        await _fixture.SeedUserAsync(ownerId, ownerEmail);

        var otherId = Guid.NewGuid();
        var otherEmail = $"other-{otherId:N}@example.com";
        await _fixture.SeedUserAsync(otherId, otherEmail);

        var food = await _fixture.SeedFoodAsync("Rice", 130m, 2.7m, 28m, 0.3m);

        using var ownerClient = _fixture.CreateAuthenticatedClient(ownerId, ownerEmail);
        var createCommand = new
        {
            Title = "Private Meal",
            Servings = 1,
            PrepTimeMinutes = 5,
            CookTimeMinutes = 10,
            IsPublic = false,
            Ingredients = new[]
            {
                new
                {
                    FoodId = food.Id,
                    IngredientText = "Rice",
                    Amount = 100m,
                    Unit = "g"
                }
            },
            Instructions = new[] { "Boil" },
            Tags = new[] { "private" }
        };

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Recipes", createCommand);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateRecipeResponse>();
        created.Should().NotBeNull();

        using var otherClient = _fixture.CreateAuthenticatedClient(otherId, otherEmail);

        var getResponse = await otherClient.GetAsync($"/api/Recipes/{created!.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var updateCommand = new
        {
            Id = created.Id,
            Title = "Hack",
            Servings = 1,
            PrepTimeMinutes = 5,
            CookTimeMinutes = 10,
            IsPublic = false,
            Ingredients = new[]
            {
                new
                {
                    FoodId = food.Id,
                    IngredientText = "Rice",
                    Amount = 100m,
                    Unit = "g"
                }
            },
            Instructions = new[] { "Boil" },
            Tags = new[] { "private" }
        };

        var updateResponse = await otherClient.PutAsJsonAsync($"/api/Recipes/{created.Id}", updateCommand);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CanCreateRecipeWithSubRecipeIngredient()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"chef-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        // Create base recipe (Tomato Sauce)
        var createSauceCommand = new
        {
            Title = "Tomato Sauce",
            Servings = 4,
            IsPublic = true,
            Ingredients = new[]
            {
                new { IngredientText = "Tomatoes", Amount = 500m, Unit = "g" },
                new { IngredientText = "Garlic", Amount = 10m, Unit = "g" }
            },
            Instructions = new[] { "Blend tomatoes", "Cook with garlic" },
            Tags = new[] { "sauce" },
            Nutrition = new
            {
                CaloriesPerServing = 50m,
                ProteinGrams = 2m,
                CarbsGrams = 8m,
                FatGrams = 1m
            }
        };

        var sauceResponse = await client.PostAsJsonAsync("/api/Recipes", createSauceCommand);
        sauceResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var sauce = await sauceResponse.Content.ReadFromJsonAsync<CreateRecipeResponse>();
        sauce.Should().NotBeNull();

        // Create recipe that uses Tomato Sauce as ingredient
        var createPastaCommand = new
        {
            Title = "Pasta with Sauce",
            Servings = 2,
            IsPublic = true,
            Ingredients = new object[]
            {
                new { FoodId = (Guid?)null, SubRecipeId = (Guid?)null, IngredientText = "Pasta", Amount = 200m, Unit = "g" },
                new { FoodId = (Guid?)null, SubRecipeId = (Guid?)sauce!.Id, IngredientText = "Tomato Sauce", Amount = 2m, Unit = "servings" }
            },
            Instructions = new[] { "Cook pasta", "Add sauce" },
            Tags = new[] { "italian" }
        };

        var pastaResponse = await client.PostAsJsonAsync("/api/Recipes", createPastaCommand);
        pastaResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var pasta = await pastaResponse.Content.ReadFromJsonAsync<CreateRecipeResponse>();
        pasta.Should().NotBeNull();

        // Verify recipe details include sub-recipe information
        var getResponse = await client.GetAsync($"/api/Recipes/{pasta!.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await getResponse.Content.ReadFromJsonAsync<RecipeDetailDto>();
        fetched.Should().NotBeNull();
        fetched!.Ingredients.Should().Contain(i => i.SubRecipeId == sauce.Id);
        fetched.Ingredients.Should().Contain(i => i.SubRecipeName == "Tomato Sauce");
    }

    [Fact]
    public async Task CannotCreateCircularDependency()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"chef-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        // Create Recipe A
        var createACommand = new
        {
            Title = "Recipe A",
            Servings = 2,
            IsPublic = true,
            Ingredients = new[] { new { IngredientText = "Ingredient A", Amount = 100m, Unit = "g" } },
            Instructions = new[] { "Step A" },
            Tags = new string[] { }
        };

        var responseA = await client.PostAsJsonAsync("/api/Recipes", createACommand);
        responseA.StatusCode.Should().Be(HttpStatusCode.Created);
        var recipeA = await responseA.Content.ReadFromJsonAsync<CreateRecipeResponse>();
        recipeA.Should().NotBeNull();

        // Create Recipe B that uses Recipe A
        var createBCommand = new
        {
            Title = "Recipe B",
            Servings = 2,
            IsPublic = true,
            Ingredients = new[]
            {
                new { SubRecipeId = recipeA!.Id, IngredientText = "Recipe A", Amount = 1m, Unit = "serving" }
            },
            Instructions = new[] { "Use Recipe A" },
            Tags = new string[] { }
        };

        var responseB = await client.PostAsJsonAsync("/api/Recipes", createBCommand);
        responseB.StatusCode.Should().Be(HttpStatusCode.Created);
        var recipeB = await responseB.Content.ReadFromJsonAsync<CreateRecipeResponse>();
        recipeB.Should().NotBeNull();

        // Try to update Recipe A to use Recipe B (would create cycle: A → B → A)
        var updateACommand = new
        {
            Id = recipeA.Id,
            Title = "Recipe A Updated",
            Servings = 2,
            IsPublic = true,
            Ingredients = new[]
            {
                new { SubRecipeId = recipeB!.Id, IngredientText = "Recipe B", Amount = 1m, Unit = "serving" }
            },
            Instructions = new[] { "Use Recipe B" },
            Tags = new string[] { }
        };

        var updateResponse = await client.PutAsJsonAsync($"/api/Recipes/{recipeA.Id}", updateACommand);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorContent = await updateResponse.Content.ReadAsStringAsync();
        errorContent.Should().Contain("circular dependency");
    }

    [Fact]
    public async Task SubRecipeNutritionIsAggregatedCorrectly()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"chef-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);

        var food = await _fixture.SeedFoodAsync("Pasta", 130m, 5m, 25m, 1m);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        // Create base recipe with known nutrition
        var createSauceCommand = new
        {
            Title = "Simple Sauce",
            Servings = 2,
            IsPublic = true,
            Ingredients = new[] { new { IngredientText = "Tomatoes", Amount = 100m, Unit = "g" } },
            Instructions = new[] { "Cook" },
            Tags = new string[] { },
            Nutrition = new
            {
                CaloriesPerServing = 40m,
                ProteinGrams = 2m,
                CarbsGrams = 6m,
                FatGrams = 1m
            }
        };

        var sauceResponse = await client.PostAsJsonAsync("/api/Recipes", createSauceCommand);
        var sauce = await sauceResponse.Content.ReadFromJsonAsync<CreateRecipeResponse>();

        // Create recipe using both food and sub-recipe
        var createMealCommand = new
        {
            Title = "Pasta Meal",
            Servings = 1,
            IsPublic = true,
            Ingredients = new object[]
            {
                new { FoodId = (Guid?)food.Id, SubRecipeId = (Guid?)null, IngredientText = "Pasta", Amount = 100m, Unit = "g" }, // 130 cal
                new { FoodId = (Guid?)null, SubRecipeId = (Guid?)sauce!.Id, IngredientText = "Simple Sauce", Amount = 2m, Unit = "servings" } // 80 cal (2 * 40)
            },
            Instructions = new[] { "Combine" },
            Tags = new string[] { }
        };

        var mealResponse = await client.PostAsJsonAsync("/api/Recipes", createMealCommand);
        mealResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var meal = await mealResponse.Content.ReadFromJsonAsync<CreateRecipeResponse>();

        // Verify nutrition calculation: (130 + 80) / 1 serving = 210 cal per serving
        var getResponse = await client.GetAsync($"/api/Recipes/{meal!.Id}");
        var fetched = await getResponse.Content.ReadFromJsonAsync<RecipeDetailDto>();

        fetched.Should().NotBeNull();
        fetched!.Nutrition.Should().NotBeNull();
        fetched.Nutrition!.CaloriesPerServing.Should().Be(210m);
    }

    private sealed record CreateRecipeResponse(Guid Id, string Title);
    private sealed record RecipeDetailResponse(Guid Id, string Title, List<string> Tags);
}
