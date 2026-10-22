using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// A person confirms an AI proposal by weight. The client sends the grams and the per-100 g
/// figures; the server does the arithmetic, so the web, the MCP app and Telegram all log the
/// same numbers for the same food (docs/AI.md).
/// </summary>
[Collection("ApiIntegration")]
public class ServerScaledMealTests
{
    private readonly ApiTestFixture _fixture;

    public ServerScaledMealTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Per100gTimesGrams_IsWhatIsStored_AndClientTotalsAreIgnored()
    {
        var user = Guid.NewGuid();
        await _fixture.SeedUserAsync(user, $"scaled-{user:N}@example.com");
        using var client = _fixture.CreateAuthenticatedClient(user, $"scaled-{user:N}@example.com");

        var response = await client.PostAsJsonAsync("/api/Meals", new
        {
            name = "Grilled chicken breast",
            entryDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            mealType = "LUNCH",
            servings = 1,
            amountGrams = 180,
            // Stale totals from a client that scaled on its own. They must not win.
            calories = 999,
            proteinGrams = 1,
            carbsGrams = 1,
            fatGrams = 1,
            per100g = new { calories = 165.33, proteinGrams = 31, carbsGrams = 0.1, fatGrams = 3.6 },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, "the API answered {0}", await response.Content.ReadAsStringAsync());

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var entry = await db.FoodDiaryEntries.AsNoTracking().SingleAsync(e => e.UserId == user);
        entry.AmountGrams.Should().Be(180);
        entry.Calories.Should().Be(297.59m);   // 165.33 * 1.8, rounded to 2 decimals like the food path
        entry.ProteinGrams.Should().Be(55.8m);
        entry.CarbsGrams.Should().Be(0.18m);
        entry.FatGrams.Should().Be(6.48m);
        entry.FiberGrams.Should().BeNull();
    }

    [Fact]
    public async Task Per100gWithoutGrams_IsRefused()
    {
        var user = Guid.NewGuid();
        await _fixture.SeedUserAsync(user, $"scaled-{user:N}@example.com");
        using var client = _fixture.CreateAuthenticatedClient(user, $"scaled-{user:N}@example.com");

        var response = await client.PostAsJsonAsync("/api/Meals", new
        {
            name = "Rice",
            entryDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            mealType = "LUNCH",
            servings = 1,
            per100g = new { calories = 130, proteinGrams = 2.7, carbsGrams = 28, fatGrams = 0.3 },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
