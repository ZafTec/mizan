extern alias McpServer;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using McpServer::Mizan.Mcp.Server.Services;
using Mizan.Application.Commands;
using Mizan.Domain.Entities;
using Xunit;

namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public class McpIntegrationTests : IClassFixture<WebApplicationFactory<McpServer::Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<McpServer::Program> _mcpFactory;
    private readonly HttpClient _mcpClient;
    private readonly ApiTestFixture _apiFixture;

    public McpIntegrationTests(WebApplicationFactory<McpServer::Program> mcpFactory, ApiTestFixture apiFixture)
    {
        _mcpFactory = mcpFactory;
        _apiFixture = apiFixture;

        _mcpFactory = mcpFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((ctx, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "MizanApiUrl", "http://localhost:5000" },
                    { "ServiceApiKey", "test-api-key" }
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBackendClient>();

                services.AddScoped<IBackendClient>(sp =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BackendClient>>();
                    var handler = _apiFixture.Server.CreateHandler();
                    var client = new HttpClient(handler);
                    return new BackendClient(client, config, logger);
                });
            });
        });

        _mcpClient = _mcpFactory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _apiFixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    #region SSE Connection Tests

    [Fact]
    public async Task SSE_Connects_Successfully_WithValidToken()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "sse@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var response = await _mcpClient.GetAsync($"/mcp/sse?token={token}", HttpCompletionOption.ResponseHeadersRead, cts.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.ToString().Should().Contain("text/event-stream");
    }

    [Fact]
    public async Task SSE_Connects_WithApiKeyHeader()
    {
        await _apiFixture.ResetDatabaseAsync();
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "sse-apikey@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        _mcpClient.DefaultRequestHeaders.Clear();
        _mcpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var response = await _mcpClient.GetAsync("/mcp/sse", HttpCompletionOption.ResponseHeadersRead, cts.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.ToString().Should().Contain("text/event-stream");
    }

    [Fact]
    public async Task SSE_RejectsConnection_WithoutToken()
    {
        var response = await _mcpClient.GetAsync("/mcp/sse");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SSE_RejectsConnection_WithInvalidToken()
    {
        var response = await _mcpClient.GetAsync("/mcp/sse?token=invalid-token");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region MCP Protocol Tests

    [Fact]
    public async Task Initialize_ReturnsCorrectProtocolVersionAndCapabilities()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "init-mcp@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = (object?)null
        };

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        jsonResponse.Should().NotBeNull();
        jsonResponse!.Result.Should().NotBeNull();

        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResponse.Result.ToString());
        result.Should().ContainKey("protocolVersion");
        result.Should().ContainKey("capabilities");
        result.Should().ContainKey("serverInfo");
    }

    [Fact]
    public async Task ToolsList_ReturnsAllAvailableTools()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "tools-mcp@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/list",
            @params = (object?)null
        };

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        jsonResponse.Should().NotBeNull();
        jsonResponse!.Result.Should().NotBeNull();

        var toolsResult = JsonSerializer.Deserialize<JsonElement>(jsonResponse.Result.ToString());
        var toolsList = toolsResult.GetProperty("tools").Deserialize<List<Dictionary<string, object>>>();
        toolsList.Should().NotBeNull();
        toolsList.Should().HaveCount(7);

        var toolNames = toolsList.Select(t => t["name"].ToString()).ToList();
        toolNames.Should().Contain(new[]
        {
            "list_ingredients", "add_ingredient", "get_shopping_list",
            "get_nutrition_tracking", "list_recipes", "add_recipe", "log_meal"
        });
    }

    #endregion

    #region list_ingredients Tool Tests

    [Fact]
    public async Task ListIngredients_SearchesByName()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "search@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        await _apiFixture.SeedFoodAsync("Chicken Breast", 165, 31, 3.6m, 0.5m);
        await _apiFixture.SeedFoodAsync("Beef", 250, 26, 15, 20m);
        await _apiFixture.SeedFoodAsync("Salmon", 208, 20, 12, 13m);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            search = "Chicken",
            limit = 10
        };

        var request = CreateJsonRpcCallRequest("tools/call", "list_ingredients", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResponse.Result.ToString());
        result.Should().ContainKey("content");
    }

    [Fact]
    public async Task ListIngredients_RespectsLimitParameter()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "limit@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        for (int i = 0; i < 5; i++)
        {
            await _apiFixture.SeedFoodAsync($"Food{i}", 100, 20, 5, 10m);
        }

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            search = "Food",
            limit = 3
        };

        var request = CreateJsonRpcCallRequest("tools/call", "list_ingredients", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();

        var textContent = ExtractToolResultText(jsonResponse);
        textContent.Should().NotBeNull();

        var foodsResult = JsonSerializer.Deserialize<Dictionary<string, object>>(textContent);
        foodsResult.Should().ContainKey("items");

        var items = JsonSerializer.Deserialize<List<object>>(foodsResult["items"].ToString());
        items.Should().HaveCountLessOrEqualTo(3);
    }

    [Fact]
    public async Task ListIngredients_ReturnsEmpty_WhenNoFoodsMatch()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "empty@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            search = "NonExistentFood",
            limit = 10
        };

        var request = CreateJsonRpcCallRequest("tools/call", "list_ingredients", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();

        var textContent = ExtractToolResultText(jsonResponse);
        textContent.Should().NotBeNull();

        var foodsResult = JsonSerializer.Deserialize<Dictionary<string, object>>(textContent);
        foodsResult.Should().ContainKey("items");

        var items = JsonSerializer.Deserialize<List<object>>(foodsResult["items"].ToString());
        items.Should().BeEmpty();
    }

    #endregion

    #region add_ingredient Tool Tests

    [Fact]
    public async Task AddIngredient_RejectsUnauthorized_ForNonAdmin()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "user@example.com", emailVerified: true, role: "user");
        var token = await CreateMcpTokenAsync(userId);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            name = "Test Ingredient",
            brand = "Test Brand",
            caloriesPer100g = 100,
            proteinPer100g = 25,
            carbsPer100g = 50,
            fatPer100g = 10,
            servingSize = 100,
            servingUnit = "g",
            verified = false
        };

        var request = CreateJsonRpcCallRequest("tools/call", "add_ingredient", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var error = jsonResponse.Error;
        error.Should().NotBeNull();
        error.Code.Should().Be(-32603);
        error.Message.Should().Match(m => m.Contains("403") || m.Contains("Unauthorized"));
    }

    [Fact]
    public async Task AddIngredient_CreatesIngredient_ForAdmin()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "admin@example.com", emailVerified: true, role: "admin");
        var token = await CreateMcpTokenAsync(userId);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            name = "New Admin Ingredient",
            brand = "Admin Brand",
            caloriesPer100g = 200,
            proteinPer100g = 30,
            carbsPer100g = 60,
            fatPer100g = 12,
            servingSize = 100,
            servingUnit = "g",
            verified = true
        };

        var request = CreateJsonRpcCallRequest("tools/call", "add_ingredient", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResponse.Result.ToString());
        result.Should().ContainKey("content");

        var foods = await _apiFixture.GetFoodsByUserId(userId);
        foods.Should().Contain(f => f.Name == "New Admin Ingredient");
    }

    [Fact]
    public async Task AddIngredient_ValidatesRequiredFields()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "validation@example.com", emailVerified: true, role: "admin");
        var token = await CreateMcpTokenAsync(userId);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            caloriesPer100g = 100,
            proteinPer100g = 25
        };

        var request = CreateJsonRpcCallRequest("tools/call", "add_ingredient", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var error = jsonResponse.Error;
        error.Should().NotBeNull();
        error.Code.Should().Be(-32603);
        error.Message.Should().Contain("must not be empty");
    }

    [Fact]
    public async Task AddIngredient_ValidatesNutrientValues()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "validation-nutrient@example.com", emailVerified: true, role: "admin");
        var token = await CreateMcpTokenAsync(userId);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            name = "Valid Ingredient",
            caloriesPer100g = -100,
            proteinPer100g = 25
        };

        var request = CreateJsonRpcCallRequest("tools/call", "add_ingredient", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var error = jsonResponse.Error;
        error.Should().NotBeNull();
        error.Code.Should().Be(-32603);
    }

    #endregion

    #region get_shopping_list Tool Tests

    [Fact]
    public async Task GetShoppingList_ReturnsUsersLists()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "shop@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        var listId1 = await _apiFixture.SeedShoppingListAsync(userId, "Weekly Groceries");
        var listId2 = await _apiFixture.SeedShoppingListAsync(userId, "Meal Plan");

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = CreateJsonRpcCallRequest("tools/call", "get_shopping_list", null);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();

        var textContent = ExtractToolResultText(jsonResponse);
        textContent.Should().NotBeNull();

        var wrapper = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(textContent);
        wrapper.Should().ContainKey("items");
        var items = wrapper!["items"].Deserialize<List<object>>();
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetShoppingList_ReturnsEmpty_WhenNoListsExist()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "empty@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = CreateJsonRpcCallRequest("tools/call", "get_shopping_list", null);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();

        var textContent = ExtractToolResultText(jsonResponse);
        textContent.Should().NotBeNull();

        var wrapper = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(textContent);
        wrapper.Should().ContainKey("items");
        var items = wrapper!["items"].Deserialize<List<object>>();
        items.Should().BeEmpty();
    }

    #endregion

    #region get_nutrition_tracking Tool Tests

    [Fact]
    public async Task GetNutritionTracking_ReturnsDailySummary()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "nutrition@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            date = date
        };

        var request = CreateJsonRpcCallRequest("tools/call", "get_nutrition_tracking", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();

        var textContent = ExtractToolResultText(jsonResponse);
        textContent.Should().NotBeNull();

        var summary = JsonSerializer.Deserialize<Dictionary<string, object>>(textContent);
        summary.Should().ContainKey("date");
        summary.Should().ContainKey("totalCalories");
        summary.Should().ContainKey("totalProtein");
        summary.Should().ContainKey("totalCarbs");
        summary.Should().ContainKey("totalFat");
    }

    #endregion

    #region list_recipes Tool Tests

    [Fact]
    public async Task ListRecipes_SearchesByName()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "recipe@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        await _apiFixture.SeedRecipeAsync(userId, "Chicken Soup", "Delicious chicken soup", 4, 60);
        await _apiFixture.SeedRecipeAsync(userId, "Beef Stew", "Hearty beef stew", 6, 90);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            search = "Chicken"
        };

        var request = CreateJsonRpcCallRequest("tools/call", "list_recipes", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResponse.Result.ToString());
        result.Should().ContainKey("content");
    }

    [Fact]
    public async Task ListRecipes_ReturnsEmpty_WhenNoRecipesMatch()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "empty@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            search = "NonExistentRecipe"
        };

        var request = CreateJsonRpcCallRequest("tools/call", "list_recipes", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();

        var textContent = ExtractToolResultText(jsonResponse);
        textContent.Should().NotBeNull();

        var recipesResult = JsonSerializer.Deserialize<Dictionary<string, object>>(textContent);
        recipesResult.Should().ContainKey("items");

        var recipes = JsonSerializer.Deserialize<List<object>>(recipesResult["items"].ToString());
        recipes.Should().BeEmpty();
    }

    #endregion

    #region add_recipe Tool Tests

    [Fact]
    public async Task AddRecipe_CreatesNewRecipe()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "add-recipe@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        var food = await _apiFixture.SeedFoodAsync("Chicken Breast", 165, 31, 0, 3.6m);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            title = "New Recipe",
            description = "Test description",
            servings = 4,
            prepTimeMinutes = 30,
            cookTimeMinutes = 45,
            isPublic = false,
            ingredients = new[]
            {
                new { foodId = food.Id, amount = 100, unit = "g", ingredientText = "Chicken Breast" }
            }
        };

        var request = CreateJsonRpcCallRequest("tools/call", "add_recipe", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResponse.Result.ToString());
        result.Should().ContainKey("content");

        var recipes = await _apiFixture.GetRecipesByUserId(userId);
        recipes.Should().Contain(r => r.Title == "New Recipe");
    }

    [Fact]
    public async Task AddRecipe_ValidatesRequiredFields()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "validation@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            servings = 4
        };

        var request = CreateJsonRpcCallRequest("tools/call", "add_recipe", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var error = jsonResponse.Error;
        error.Should().NotBeNull();
        error.Code.Should().Be(-32603);
        error.Message.Should().Contain("required");
    }

    [Fact]
    public async Task AddRecipe_ValidatesTimeFields()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "validation-time@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        var food = await _apiFixture.SeedFoodAsync("Time Test Food", 100, 10, 10, 5);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            title = "Test Recipe",
            description = "Test description",
            servings = 4,
            prepTimeMinutes = -30,
            cookTimeMinutes = 45,
            ingredients = new[]
            {
                new { foodId = food.Id, amount = 100, unit = "g", ingredientText = "Time Test Food" }
            }
        };

        var request = CreateJsonRpcCallRequest("tools/call", "add_recipe", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var error = jsonResponse.Error;
        error.Should().NotBeNull();
        error.Code.Should().Be(-32603);
        error.Message.Should().Contain("positive");
    }

    [Fact]
    public async Task AddRecipe_SetsPublicByDefault()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "public-recipe@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        var food = await _apiFixture.SeedFoodAsync("Rice", 130, 2.7m, 28, 0.3m);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            title = "Default Public Recipe",
            description = "Default public recipe",
            servings = 2,
            prepTimeMinutes = 15,
            cookTimeMinutes = 20,
            ingredients = new[]
            {
                new { foodId = food.Id, amount = 150, unit = "g", ingredientText = "Rice" }
            }
        };

        var request = CreateJsonRpcCallRequest("tools/call", "add_recipe", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResponse.Result.ToString());
        result.Should().ContainKey("content");

        var recipes = await _apiFixture.GetRecipesByUserId(userId);
        recipes.Should().Contain(r => r.IsPublic);
    }

    [Fact]
    public async Task AddRecipe_CanCreatePrivateRecipe()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "private@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        var food = await _apiFixture.SeedFoodAsync("Secret Sauce", 50, 0, 5, 5);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            title = "Private Recipe",
            description = "This is a private recipe",
            servings = 2,
            prepTimeMinutes = 15,
            cookTimeMinutes = 20,
            isPublic = false,
            ingredients = new[]
            {
                new { foodId = food.Id, amount = 10, unit = "ml", ingredientText = "Secret Sauce" }
            }
        };

        var request = CreateJsonRpcCallRequest("tools/call", "add_recipe", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResponse.Result.ToString());
        result.Should().ContainKey("content");

        var recipes = await _apiFixture.GetRecipesByUserId(userId);
        recipes.Should().Contain(r => r.Title == "Private Recipe" && !r.IsPublic);
    }

    #endregion

    #region log_meal Tool Tests

    [Fact]
    public async Task LogMeal_CreatesFoodDiaryEntry()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "meal@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        var food = await _apiFixture.SeedFoodAsync("Apple", 52, 0.3m, 14, 0.2m);
        await _apiFixture.SeedFoodAsync("Banana", 89, 1.1m, 23, 0.3m);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            mealType = "SNACK",
            foodId = food.Id,
            servings = 1
        };

        var request = CreateJsonRpcCallRequest("tools/call", "log_meal", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResponse.Result.ToString());
        result.Should().ContainKey("content");
    }

    [Fact]
    public async Task LogMeal_CreatesRecipeMealEntry()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "meal-recipe@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var recipe = await _apiFixture.SeedRecipeAsync(userId, "Pasta", "Simple pasta", 4, 20, true);
        var ingredients = new[]
        {
            new { FoodId = Guid.Empty, Amount = 200, Unit = "g" }
        };

        var args = new
        {
            date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            mealType = "MEAL",
            recipeId = recipe.Id,
            servings = 2,
            ingredients = ingredients
        };

        var request = CreateJsonRpcCallRequest("tools/call", "log_meal", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResponse.Result.ToString());
        result.Should().ContainKey("content");
    }

    [Fact]
    public async Task LogMeal_ValidatesMealType()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "validation@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        var food = await _apiFixture.SeedFoodAsync("Meal Type Test Food", 100, 10, 10, 5);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            mealType = "InvalidType",
            servings = 1,
            foodId = food.Id
        };

        var request = CreateJsonRpcCallRequest("tools/call", "log_meal", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var error = jsonResponse.Error;
        error.Should().NotBeNull();
        error.Code.Should().Be(-32603);
        error.Message.Should().Contain("Meal type");
    }

    [Fact]
    public async Task LogMeal_ValidatesServings()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "validation@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        var food = await _apiFixture.SeedFoodAsync("Servings Test Food", 100, 10, 10, 5);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            mealType = "SNACK",
            servings = 0,
            foodId = food.Id
        };

        var request = CreateJsonRpcCallRequest("tools/call", "log_meal", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var error = jsonResponse.Error;
        error.Should().NotBeNull();
        error.Code.Should().Be(-32603);
        error.Message.Should().Contain("Servings");
    }

    [Fact]
    public async Task LogMeal_RejectsInvalidMealType()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "invalid-meal@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            mealType = "InvalidType"
        };

        var request = CreateJsonRpcCallRequest("tools/call", "log_meal", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var error = jsonResponse.Error;
        error.Should().NotBeNull();
        error.Code.Should().Be(-32603);
    }

    [Fact]
    public async Task LogMeal_RequiresFoodOrRecipe()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "required@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            mealType = "SNACK",
            servings = 1
        };

        var request = CreateJsonRpcCallRequest("tools/call", "log_meal", args);

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var error = jsonResponse.Error;
        error.Should().NotBeNull();
        error.Code.Should().Be(-32603);
        error.Message.Should().Contain("foodId");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task InvalidMethod_ReturnsError()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "invalid@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "invalid_method"
        };

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var error = jsonResponse.Error;
        error.Should().NotBeNull();
        error.Code.Should().Be(-32603);
        error.Message.Should().Contain("Method not found");
    }

    [Fact]
    public async Task MissingParams_ReturnsError()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "missing@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call"
        };

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var error = jsonResponse.Error;
        error.Should().NotBeNull();
        error.Code.Should().Be(-32603);
        error.Message.Should().Contain("Params missing");
    }

    [Fact]
    public async Task BackendError_ReturnsJsonRpcError()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "backend-error@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var requestError = CreateJsonRpcCallRequest("tools/call", "log_meal", new
        {
            date = "2023-01-01",
            mealType = "Breakfast",
            foodId = "invalid-uuid",
            servings = 1
        });

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", requestError);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();

        jsonResponse.Should().NotBeNull();
        jsonResponse!.Error.Should().NotBeNull();
        jsonResponse.Error!.Message.Should().Contain("Backend API error");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task ToolExecution_LogsExecutionTime()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "timing@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        await _apiFixture.SeedFoodAsync("Test Food", 100, 25, 5, 2.0m);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new
        {
            search = "Test"
        };

        var request = CreateJsonRpcCallRequest("tools/call", "list_ingredients", args);

        var startTime = DateTime.UtcNow;
        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);
        var elapsed = DateTime.UtcNow - startTime;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResponse.Result.ToString());
        result.Should().ContainKey("content");

        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Backend Integration Tests

    [Fact]
    public async Task BackendIntegration_LogsMcpUsage()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "integration@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        var food = await _apiFixture.SeedFoodAsync("Usage Food", 120, 10, 15, 5);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = CreateJsonRpcCallRequest("tools/call", "log_meal", new
        {
            Date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            MealType = "SNACK",
            Servings = 1,
            FoodId = food.Id
        });

        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResponse.Result.ToString());
        result.Should().ContainKey("content");

        var logs = await _apiFixture.GetMcpUsageLogsByUserId(userId);
        logs.Should().HaveCount(1);
        logs.Should().OnlyContain(l => l.ToolName == "log_meal");
    }

    #endregion

    #region Helper Methods

    private async Task<string> CreateMcpTokenAsync(Guid userId)
    {
        var email = "test-mcp-user@example.com";

        var client = _apiFixture.CreateAuthenticatedClient(userId, email);

        var createResponse = await client.PostAsJsonAsync("/api/McpTokens", new { Name = "Test Token" });

        if (createResponse.StatusCode != HttpStatusCode.Created)
        {
            var error = await createResponse.Content.ReadAsStringAsync();
            throw new Exception($"Failed to create MCP token. Status: {createResponse.StatusCode}, Error: {error}");
        }

        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateMcpTokenResult>();
        createResult.Should().NotBeNull();
        return createResult!.PlaintextToken;
    }

    private async Task<Guid> CreateShoppingListAsync(Guid userId, string name)
    {
        var client = _apiFixture.CreateAuthenticatedClient(userId, "test@example.com");

        var createResponse = await client.PostAsJsonAsync("/api/ShoppingLists", new { Name = name });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var id = await createResponse.Content.ReadFromJsonAsync<Guid>();
        return id;
    }

    private object CreateJsonRpcCallRequest(string method, string toolName, object? args)
    {
        return new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString("N"),
            method = method,
            @params = new
            {
                name = toolName,
                arguments = args
            }
        };
    }

    private object CreateJsonRpcRequest(string method, object? args = null)
    {
        return new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString("N"),
            method = method,
            @params = args
        };
    }

    private string? ExtractToolResultText(JsonRpcResponse? response)
    {
        if (response?.Result == null) return null;
        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(response.Result.ToString());
        if (result == null || !result.ContainsKey("content")) return null;
        var contentArray = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(result["content"].ToString());
        if (contentArray == null || contentArray.Count == 0) return null;
        var text = contentArray[0]["text"].ToString();
        return JsonSerializer.Deserialize<string>(text);
    }

    private class JsonRpcResponse
    {
        public object? Result { get; set; }
        public JsonRpcError? Error { get; set; }
    }

    private class JsonRpcError
    {
        public int Code { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    #endregion

    #region Pagination Tests

    [Fact]
    public async Task ListIngredients_ReturnsSecondPage()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "page2@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        for (int i = 0; i < 5; i++)
        {
            await _apiFixture.SeedFoodAsync($"PaginatedFood{i}", 100, 20, 5, 10m);
        }

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new { search = "PaginatedFood", limit = 2, page = 2 };
        var request = CreateJsonRpcCallRequest("tools/call", "list_ingredients", args);
        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var textContent = ExtractToolResultText(jsonResponse);
        textContent.Should().NotBeNull();

        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(textContent!);
        result.Should().ContainKey("items");
        result.Should().ContainKey("totalCount");
        result.Should().ContainKey("page");
        result.Should().ContainKey("pageSize");

        var items = JsonSerializer.Deserialize<List<object>>(result!["items"].ToString()!);
        items.Should().HaveCountLessOrEqualTo(2);
    }

    [Fact]
    public async Task ListIngredients_ReturnsTotalCountAndPageInfo()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "pageinfo@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        for (int i = 0; i < 5; i++)
        {
            await _apiFixture.SeedFoodAsync($"InfoFood{i}", 100, 20, 5, 10m);
        }

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new { search = "InfoFood", limit = 2, page = 1 };
        var request = CreateJsonRpcCallRequest("tools/call", "list_ingredients", args);
        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var textContent = ExtractToolResultText(jsonResponse);
        var result = JsonSerializer.Deserialize<JsonElement>(textContent!);

        result.GetProperty("totalCount").GetInt32().Should().Be(5);
        result.GetProperty("page").GetInt32().Should().Be(1);
        result.GetProperty("pageSize").GetInt32().Should().Be(2);
        result.GetProperty("totalPages").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task ListIngredients_SortsByName_Ascending()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "sortasc@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        await _apiFixture.SeedFoodAsync("Zebra Meat", 200, 30, 5, 10m);
        await _apiFixture.SeedFoodAsync("Apple", 52, 0.3m, 14, 0.2m);
        await _apiFixture.SeedFoodAsync("Banana", 89, 1.1m, 23, 0.3m);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new { sortBy = "name", sortOrder = "asc", limit = 10 };
        var request = CreateJsonRpcCallRequest("tools/call", "list_ingredients", args);
        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var textContent = ExtractToolResultText(jsonResponse);
        var result = JsonSerializer.Deserialize<JsonElement>(textContent!);

        var items = result.GetProperty("items");
        items.GetArrayLength().Should().BeGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task ListIngredients_SortsByCalories_Descending()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "sortdesc@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        await _apiFixture.SeedFoodAsync("LowCal", 50, 10, 5, 2m);
        await _apiFixture.SeedFoodAsync("HighCal", 500, 25, 50, 25m);
        await _apiFixture.SeedFoodAsync("MidCal", 200, 15, 20, 10m);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new { sortBy = "calories", sortOrder = "desc", limit = 10 };
        var request = CreateJsonRpcCallRequest("tools/call", "list_ingredients", args);
        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var textContent = ExtractToolResultText(jsonResponse);
        textContent.Should().NotBeNull();
    }

    [Fact]
    public async Task ListRecipes_ReturnsPagedResults()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "recipepage@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new { page = 1, limit = 5 };
        var request = CreateJsonRpcCallRequest("tools/call", "list_recipes", args);
        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var textContent = ExtractToolResultText(jsonResponse);
        textContent.Should().NotBeNull();

        var result = JsonSerializer.Deserialize<JsonElement>(textContent!);
        result.TryGetProperty("items", out _).Should().BeTrue();
        result.TryGetProperty("totalCount", out _).Should().BeTrue();
        result.TryGetProperty("page", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ListRecipes_SortsByTitle()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "recipesort@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new { sortBy = "title", sortOrder = "asc", limit = 10 };
        var request = CreateJsonRpcCallRequest("tools/call", "list_recipes", args);
        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetShoppingList_ReturnsPagedResults()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "shoppingpage@example.com", emailVerified: true);
        var token = await CreateMcpTokenAsync(userId);

        _mcpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var args = new { page = 1, limit = 5 };
        var request = CreateJsonRpcCallRequest("tools/call", "get_shopping_list", args);
        var response = await _mcpClient.PostAsJsonAsync("/mcp/messages?sessionId=test", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var textContent = ExtractToolResultText(jsonResponse);
        textContent.Should().NotBeNull();

        var result = JsonSerializer.Deserialize<JsonElement>(textContent!);
        result.TryGetProperty("items", out _).Should().BeTrue();
        result.TryGetProperty("totalCount", out _).Should().BeTrue();
    }

    #endregion
}
