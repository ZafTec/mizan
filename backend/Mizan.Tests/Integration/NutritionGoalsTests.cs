using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public class NutritionGoalsTests
{
    private readonly ApiTestFixture _fixture;

    public NutritionGoalsTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task LogFood_ReturnsDailyTotals_AndGoalTargets()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"nutrition-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);

        var food = await _fixture.SeedFoodAsync("Test Food", 100m, 10m, 20m, 5m);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var goalCommand = new
        {
            GoalType = "general",
            TargetCalories = 2000,
            TargetProteinGrams = 150m,
            TargetCarbsGrams = 250m,
            TargetFatGrams = 60m
        };

        var goalResponse = await client.PostAsJsonAsync("/api/Goals", goalCommand);
        goalResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var entryDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var logCommand = new
        {
            FoodId = food.Id,
            EntryDate = entryDate,
            MealType = "lunch",
            Servings = 2m
        };

        var logResponse = await client.PostAsJsonAsync("/api/Nutrition/log", logCommand);
        logResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var dailyResponse = await client.GetAsync($"/api/Nutrition/daily?date={entryDate:yyyy-MM-dd}");
        dailyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var daily = await dailyResponse.Content.ReadFromJsonAsync<DailyNutritionResponse>();
        daily.Should().NotBeNull();
        daily!.TotalCalories.Should().Be(200m);
        daily.TotalProtein.Should().Be(20m);
        daily.TotalCarbs.Should().Be(40m);
        daily.TotalFat.Should().Be(10m);
        daily.TargetCalories.Should().Be(2000m);
        daily.TargetProtein.Should().Be(150m);
    }

    [Fact]
    public async Task RecordGoalProgress_ReturnsHistory()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"goals-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var goalCommand = new
        {
            GoalType = "general",
            TargetCalories = 1800,
            TargetProteinGrams = 120m,
            TargetCarbsGrams = 200m,
            TargetFatGrams = 50m
        };

        var goalResponse = await client.PostAsJsonAsync("/api/Goals", goalCommand);
        goalResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var progressDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var progressCommand = new
        {
            ActualCalories = 1750,
            ActualProteinGrams = 110m,
            ActualCarbsGrams = 190m,
            ActualFatGrams = 45m,
            Date = progressDate
        };

        var progressResponse = await client.PostAsJsonAsync("/api/Goals/progress", progressCommand);
        progressResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var historyResponse = await client.GetAsync("/api/Goals/progress?days=7");
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await historyResponse.Content.ReadFromJsonAsync<GoalProgressHistoryResponse>();
        history.Should().NotBeNull();
        history!.ProgressEntries.Should().HaveCount(1);
        history.ProgressEntries[0].ActualCalories.Should().Be(1750m);
    }

    private sealed record DailyNutritionResponse(
        DateOnly Date,
        decimal TotalCalories,
        decimal TotalProtein,
        decimal TotalCarbs,
        decimal TotalFat,
        decimal? TargetCalories,
        decimal? TargetProtein
    );

    private sealed record GoalProgressHistoryResponse(GoalSummaryResponse? Goal, List<GoalProgressEntryResponse> ProgressEntries);
    private sealed record GoalSummaryResponse(decimal? TargetCalories, decimal? TargetProteinGrams, decimal? TargetCarbsGrams, decimal? TargetFatGrams);
    private sealed record GoalProgressEntryResponse(Guid Id, DateOnly Date, decimal ActualCalories, decimal ActualProteinGrams, decimal ActualCarbsGrams, decimal ActualFatGrams);
}
