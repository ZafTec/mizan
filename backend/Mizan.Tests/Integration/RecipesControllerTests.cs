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

    private sealed record CreateRecipeResponse(Guid Id, string Title);
    private sealed record RecipeDetailResponse(Guid Id, string Title, List<string> Tags);
}
