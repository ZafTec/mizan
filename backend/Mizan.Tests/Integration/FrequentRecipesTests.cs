using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>The recipes a person logs most come first when they log from a recipe, and only their own logging counts.</summary>
[Collection("ApiIntegration")]
public class FrequentRecipesTests
{
    private readonly ApiTestFixture _fixture;

    public FrequentRecipesTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FrequentRecipes_AreOrderedByHowOftenTheViewerLoggedThem_AndLeaveOutTheRest()
    {
        await _fixture.ResetDatabaseAsync();
        var (user, client) = await UserAsync();
        var often = await _fixture.SeedRecipeAsync(user, "Often", "d", 2, 10, false);
        var once = await _fixture.SeedRecipeAsync(user, "Once", "d", 2, 10, false);
        await _fixture.SeedRecipeAsync(user, "Never", "d", 2, 10, false);
        await LogAsync(user, often.Id, 3);
        await LogAsync(user, once.Id, 1);

        var page = await client.GetFromJsonAsync<JsonElement>("/api/Recipes?SortBy=frequent&PageSize=10");

        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("title").GetString()).Should().Equal("Often", "Once");
        items.Select(i => i.GetProperty("timesLogged").GetInt32()).Should().Equal(3, 1);
        page.GetProperty("totalCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task AnotherPersonsLogging_DoesNotCount()
    {
        await _fixture.ResetDatabaseAsync();
        var (user, client) = await UserAsync();
        var (other, _) = await UserAsync();
        var shared = await _fixture.SeedRecipeAsync(other, "Shared", "d", 2, 10, true);
        await LogAsync(other, shared.Id, 5);

        var page = await client.GetFromJsonAsync<JsonElement>("/api/Recipes?SortBy=frequent&PageSize=10");

        page.GetProperty("items").GetArrayLength().Should().Be(0, "this person never logged it");
    }

    [Fact]
    public async Task EveryRecipeCarriesItsCount_InTheOrdinaryList()
    {
        await _fixture.ResetDatabaseAsync();
        var (user, client) = await UserAsync();
        var recipe = await _fixture.SeedRecipeAsync(user, "Counted", "d", 2, 10, false);
        await LogAsync(user, recipe.Id, 2);

        var page = await client.GetFromJsonAsync<JsonElement>("/api/Recipes?PageSize=10");

        page.GetProperty("items")[0].GetProperty("timesLogged").GetInt32().Should().Be(2);
    }

    private async Task<(Guid User, HttpClient Client)> UserAsync()
    {
        var id = Guid.NewGuid();
        var email = $"freq-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email);
        return (id, _fixture.CreateAuthenticatedClient(id, email));
    }

    private async Task LogAsync(Guid user, Guid recipe, int times)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        for (var i = 0; i < times; i++)
            db.FoodDiaryEntries.Add(new FoodDiaryEntry
            {
                Id = Guid.NewGuid(), UserId = user, RecipeId = recipe, Name = "x", EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
                MealType = "lunch", Servings = 1, LoggedAt = DateTime.UtcNow.AddMinutes(-i),
            });
        await db.SaveChangesAsync();
    }
}
