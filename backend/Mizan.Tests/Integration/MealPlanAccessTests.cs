using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>A meal plan can only hold recipes the person could open themselves.</summary>
[Collection("ApiIntegration")]
public class MealPlanAccessTests
{
    private readonly ApiTestFixture _fixture;

    public MealPlanAccessTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CreatingAPlan_WithSomeoneElsesPrivateRecipe_IsRefusedAndSavesNothing()
    {
        await _fixture.ResetDatabaseAsync();
        var (me, client) = await UserAsync();
        var (other, _) = await UserAsync();
        var mine = await _fixture.SeedRecipeAsync(me, "Mine", "d", 2, 10, false);
        var theirs = await _fixture.SeedRecipeAsync(other, "Theirs", "d", 2, 10, false);

        var refused = await client.PostAsJsonAsync("/api/MealPlans", Plan(mine.Id, theirs.Id));
        var allowed = await client.PostAsJsonAsync("/api/MealPlans", Plan(mine.Id));

        refused.IsSuccessStatusCode.Should().BeFalse();
        allowed.IsSuccessStatusCode.Should().BeTrue();
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        (await db.MealPlans.CountAsync(p => p.UserId == me)).Should().Be(1);
        (await db.MealPlanRecipes.AnyAsync(r => r.RecipeId == theirs.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task CreatingAPlan_WithAPublicRecipe_Works()
    {
        await _fixture.ResetDatabaseAsync();
        var (_, client) = await UserAsync();
        var (other, _) = await UserAsync();
        var shared = await _fixture.SeedRecipeAsync(other, "Shared", "d", 2, 10, true);

        var response = await client.PostAsJsonAsync("/api/MealPlans", Plan(shared.Id));

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    private static object Plan(params Guid[] recipes) => new
    {
        name = "Week",
        startDate = "2026-10-12",
        endDate = "2026-10-18",
        recipes = recipes.Select(id => new { recipeId = id, date = "2026-10-12", mealType = "dinner", servings = 1 }),
    };

    private async Task<(Guid User, HttpClient Client)> UserAsync()
    {
        var id = Guid.NewGuid();
        var email = $"plan-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email);
        return (id, _fixture.CreateAuthenticatedClient(id, email));
    }
}
