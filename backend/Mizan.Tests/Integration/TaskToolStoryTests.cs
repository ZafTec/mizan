using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Contracts.Mcp;
using Mizan.Domain.Entities;
using Mizan.Domain.Identity;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The task tools through the API (docs/MCP.md#task-tools): what a caller is offered, a call,
/// the refusals that fail closed, and the preview and confirm step of a high-impact delete.
/// </summary>
[Collection("ApiIntegration")]
public class TaskToolStoryTests
{
    private readonly ApiTestFixture _fixture;

    public TaskToolStoryTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task APerson_IsOfferedTheTwentySixPersonalAndHouseholdTools_AndTheAssistantFifteen()
    {
        await _fixture.ResetDatabaseAsync();
        var user = Guid.NewGuid();
        await _fixture.SeedUserAsync(user, $"u-{user:N}@example.com");
        using var client = _fixture.CreateAuthenticatedClient(user, "u@example.com");

        var mcp = await ManifestAsync(client, "mcp");
        mcp.Should().HaveCount(26).And.Contain(["find_food", "log_food", "get_body_trend", "delete", "manage_household"]);
        mcp.Should().NotContain(name => name.StartsWith("admin_") || name == "get_clients");

        var assistant = await ManifestAsync(client, "assistant");
        assistant.Should().HaveCount(15).And.NotContain(["get_social", "social_action", "get_my_coach", "update_my_settings"]);

        (await client.GetAsync("/api/tools?surface=telegram")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AConnection_SeesOnlyTheToolsAndModesOfTheAreasItWasGiven()
    {
        await _fixture.ResetDatabaseAsync();
        var user = Guid.NewGuid();
        await _fixture.SeedUserAsync(user, $"u-{user:N}@example.com");

        using var body = AppClient(user, await GrantAsync(user, [McpScopes.BodyRead]));
        (await ManifestAsync(body, "mcp")).Should().BeEquivalentTo(["get_body_trend", "get_my_account"]);

        // A grant made before the areas reaches the modes that replace its tools, and no more.
        using var legacy = AppClient(user, await GrantAsync(user, ["recipes:read"]));
        var manifest = await legacy.GetFromJsonAsync<JsonElement>("/api/tools?surface=mcp");
        var findFood = manifest.GetProperty("tools").EnumerateArray().Single(t => t.GetProperty("name").GetString() == "find_food");
        findFood.GetProperty("inputSchema").GetProperty("properties").GetProperty("kind").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()).Should().BeEquivalentTo(["recipe"]);
    }

    [Fact]
    public async Task ACall_RunsTheCommand_AndACallOutsideTheGrantIsRefusedNamingTheArea()
    {
        await _fixture.ResetDatabaseAsync();
        var user = Guid.NewGuid();
        await _fixture.SeedUserAsync(user, $"u-{user:N}@example.com");
        using var app = AppClient(user, await GrantAsync(user, [McpScopes.BodyWrite]));

        var logged = await CallAsync(app, "log_body", new { date = "2026-10-01", weightKg = 81.5 });
        logged.GetProperty("isError").GetBoolean().Should().BeFalse();
        var trend = await CallAsync(app, "get_body_trend", new { });
        trend.GetProperty("isError").GetBoolean().Should().BeFalse();
        trend.GetProperty("data").ToString().Should().Contain("81.5");

        var refused = await CallAsync(app, "log_food", new { items = new[] { new { name = "Toast", calories = 120 } } });
        refused.GetProperty("isError").GetBoolean().Should().BeTrue();
        refused.GetProperty("errorCode").GetString().Should().Be("not_allowed");
        refused.GetProperty("needs").GetString().Should().Be(McpScopes.NutritionWrite);

        var admin = await CallAsync(app, "admin_lookup", new { });
        admin.GetProperty("errorCode").GetString().Should().Be("not_allowed");
        admin.GetProperty("needs").GetString().Should().Be("role:admin");
    }

    [Fact]
    public async Task AConnectedApp_ReadsThePersonsOwnRecipes_EvenWhenItFillsUnusedArgumentsWithEmptyValues()
    {
        await _fixture.ResetDatabaseAsync();
        var user = Guid.NewGuid();
        await _fixture.SeedUserAsync(user, $"u-{user:N}@example.com");
        await _fixture.SeedRecipeAsync(user, "Lentil soup", "Red lentils", 4, 30);
        using var app = AppClient(user, await GrantAsync(user, [McpScopes.NutritionRead]));

        // A model often sends every argument of the schema; an empty string or the zero id means it left one out.
        var found = await CallAsync(app, "find_food", new
        {
            kind = "recipe",
            query = "lentil",
            recipeId = Guid.Empty,
            foodId = "",
        });

        found.GetProperty("isError").GetBoolean().Should().BeFalse(found.ToString());
        found.GetProperty("text").GetString().Should().Contain("Lentil soup");
    }

    [Fact]
    public async Task DeletingARecipe_ShowsWhatGoes_AndRunsOnlyOnceWhenConfirmed()
    {
        await _fixture.ResetDatabaseAsync();
        var user = Guid.NewGuid();
        await _fixture.SeedUserAsync(user, $"u-{user:N}@example.com");
        var recipe = await _fixture.SeedRecipeAsync(user, "Lentil soup", "Red lentils", 4, 30);
        using var client = _fixture.CreateAuthenticatedClient(user, "u@example.com");

        var preview = await CallAsync(client, "delete", new { kind = "recipe", id = recipe.Id });
        preview.GetProperty("isError").GetBoolean().Should().BeFalse();
        preview.GetProperty("confirmation").GetProperty("preview").ToString().Should().Contain("Lentil soup");
        var token = preview.GetProperty("confirmation").GetProperty("token").GetString()!;
        (await RecipeExistsAsync(recipe.Id)).Should().BeTrue("a preview removes nothing");

        var done = await CallAsync(client, "delete", new { kind = "confirm", token });
        done.GetProperty("isError").GetBoolean().Should().BeFalse(done.ToString());
        (await RecipeExistsAsync(recipe.Id)).Should().BeFalse();

        var again = await CallAsync(client, "delete", new { kind = "confirm", token });
        again.GetProperty("errorCode").GetString().Should().Be("token_used");

        var stranger = Guid.NewGuid();
        await _fixture.SeedUserAsync(stranger, $"s-{stranger:N}@example.com");
        using var other = _fixture.CreateAuthenticatedClient(stranger, "s@example.com");
        (await CallAsync(other, "delete", new { kind = "confirm", token })).GetProperty("errorCode").GetString().Should().Be("invalid_token");
    }

    [Fact]
    public async Task AConfirmation_IsRefused_WhenItExpired_OrWhatThePreviewShowedChanged()
    {
        await _fixture.ResetDatabaseAsync();
        var user = Guid.NewGuid();
        await _fixture.SeedUserAsync(user, $"u-{user:N}@example.com");
        var stale = await _fixture.SeedRecipeAsync(user, "Stale soup", "", 2, 10);
        var changed = await _fixture.SeedRecipeAsync(user, "Bean stew", "", 2, 10);
        using var client = _fixture.CreateAuthenticatedClient(user, "u@example.com");

        var staleToken = (await CallAsync(client, "delete", new { kind = "recipe", id = stale.Id }))
            .GetProperty("confirmation").GetProperty("token").GetString()!;
        await WithDbAsync(db => db.PendingToolActions.Where(p => p.UserId == user)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ExpiresAt, DateTime.UtcNow.AddMinutes(-1))));
        (await CallAsync(client, "delete", new { kind = "confirm", token = staleToken }))
            .GetProperty("errorCode").GetString().Should().Be("token_expired");

        var changedToken = (await CallAsync(client, "delete", new { kind = "recipe", id = changed.Id }))
            .GetProperty("confirmation").GetProperty("token").GetString()!;
        await WithDbAsync(db => db.Recipes.Where(r => r.Id == changed.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Title, "Bean and chorizo stew")));
        (await CallAsync(client, "delete", new { kind = "confirm", token = changedToken }))
            .GetProperty("errorCode").GetString().Should().Be("facts_changed");

        (await RecipeExistsAsync(stale.Id)).Should().BeTrue();
        (await RecipeExistsAsync(changed.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task TheReference_ListsPriceRegionsAndTheFixedValuesToolsTake()
    {
        await _fixture.ResetDatabaseAsync();
        var user = Guid.NewGuid();
        await _fixture.SeedUserAsync(user, $"u-{user:N}@example.com");
        using var app = AppClient(user, await GrantAsync(user, [McpScopes.BodyRead]));

        var reference = await app.GetFromJsonAsync<JsonElement>("/api/tools/reference");
        reference.GetProperty("mealSlots").GetArrayLength().Should().BeGreaterThan(0);
        reference.GetProperty("goalTypes").GetArrayLength().Should().BeGreaterThan(0);
        reference.GetProperty("reminderKinds").GetArrayLength().Should().BeGreaterThan(0);
        reference.TryGetProperty("priceRegions", out _).Should().BeTrue();
    }

    private static async Task<List<string>> ManifestAsync(HttpClient client, string surface)
    {
        var manifest = await client.GetFromJsonAsync<JsonElement>($"/api/tools?surface={surface}");
        return manifest.GetProperty("tools").EnumerateArray().Select(t => t.GetProperty("name").GetString()!).ToList();
    }

    private static async Task<JsonElement> CallAsync(HttpClient client, string tool, object arguments)
    {
        var response = await client.PostAsJsonAsync($"/api/tools/{tool}/call", new { surface = "mcp", arguments });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<bool> RecipeExistsAsync(Guid id)
    {
        using var scope = _fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<MizanDbContext>().Recipes.AnyAsync(r => r.Id == id);
    }

    private async Task WithDbAsync(Func<MizanDbContext, Task> action)
    {
        using var scope = _fixture.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<MizanDbContext>());
    }

    private async Task<Guid> GrantAsync(Guid user, string[] scopes)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var client = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = "mzc_" + SecureToken.Generate()[..16],
            Name = "Test Agent",
            RedirectUris = ["http://localhost:1/cb"],
            Source = OAuthClient.SourceDynamic,
            CreatedAt = DateTime.UtcNow,
        };
        var grant = new OAuthGrant
        {
            Id = Guid.NewGuid(),
            UserId = user,
            ClientId = client.Id,
            Scopes = McpScopes.Normalize(scopes, allowAdmin: false),
            HouseholdMode = "none",
            HouseholdIds = [],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.OAuthClients.Add(client);
        db.OAuthGrants.Add(grant);
        await db.SaveChangesAsync();
        return grant.Id;
    }

    /// <summary>The way the MCP service calls the API for a connected app.</summary>
    private HttpClient AppClient(Guid user, Guid grant)
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
        client.DefaultRequestHeaders.Add("X-Impersonate-User", user.ToString());
        client.DefaultRequestHeaders.Add("X-Mcp-Grant", grant.ToString());
        return client;
    }
}
