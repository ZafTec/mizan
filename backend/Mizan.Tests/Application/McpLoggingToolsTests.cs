extern alias McpServer;
using System.Net;
using System.Reflection;
using System.Text.Json;
using McpServer::Mizan.Mcp.Server.Services;
using McpServer::Mizan.Mcp.Server.Tools;
using Mizan.Contracts.Meals;
using Mizan.Contracts.Recipes;
using Mizan.Contracts.Workouts;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;
using Xunit;

namespace Mizan.Tests.Application;

public class McpLoggingToolsTests
{
    [Fact]
    public async Task LogDayWritesMealsAndSetsForOneDateWithoutDuplicatingTheWorkoutWeight()
    {
        var api = new Mock<IBackendApiClient>();
        var calls = new List<(string Endpoint, object? Body)>();
        api.Setup(client => client.PostAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, CancellationToken>((endpoint, body, _) => calls.Add((endpoint, body)))
            .ReturnsAsync("{\"id\":\"saved\"}");

        var result = await new MealTools(api.Object).LogDay("2026-09-05",
            mealsJson: JsonSerializer.Serialize(new[] { new { foodId = Guid.NewGuid(), mealType = "lunch", servings = 1.5m } }),
            exercisesJson: JsonSerializer.Serialize(new[] { new { exerciseId = Guid.NewGuid(), sets = new[] { new { reps = 8, weightKg = 60 } } } }),
            weightKg: 74, workoutName: "Strength");

        Assert.False(result.IsError);
        Assert.Equal(2, calls.Count);
        Assert.Equal("/api/Meals", calls[0].Endpoint);
        var meal = Assert.IsType<LogMealRequest>(calls[0].Body);
        Assert.Equal(new DateOnly(2026, 9, 5), meal.EntryDate);
        Assert.Equal("LUNCH", meal.MealType);
        var workout = Assert.IsType<LogWorkoutRequest>(calls[1].Body);
        Assert.Equal(meal.EntryDate, workout.WorkoutDate);
        Assert.Equal(74, workout.BodyweightKg);
        Assert.Equal(8, Assert.Single(Assert.Single(workout.Exercises).Sets).Reps);
        api.Verify(client => client.PostAsync("/api/BodyMeasurements", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LogDayRejectsInvalidLaterInputBeforeWritingAnyEarlierMeal()
    {
        var api = new Mock<IBackendApiClient>();
        var action = () => new MealTools(api.Object).LogDay("2026-09-05",
            mealsJson: JsonSerializer.Serialize(new[] { new { foodId = Guid.NewGuid(), mealType = "LUNCH" } }),
            exercisesJson: "[{\"exerciseId\":\"00000000-0000-0000-0000-000000000000\",\"sets\":[]}]");

        await Assert.ThrowsAsync<ArgumentException>(action);
        api.Verify(client => client.PostAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LogDayReturnsCompletedAndRemainingItemsWhenAWriteIsRejected()
    {
        var api = new Mock<IBackendApiClient>();
        api.SetupSequence(client => client.PostAsync("/api/Meals", It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"id\":\"first-meal\"}")
            .ThrowsAsync(new BackendApiException(HttpStatusCode.BadRequest, "invalid_food", "Food is unavailable"));
        var result = await new MealTools(api.Object).LogDay("2026-09-05", mealsJson: JsonSerializer.Serialize(new[]
        {
            new { foodId = Guid.NewGuid(), mealType = "LUNCH" },
            new { foodId = Guid.NewGuid(), mealType = "LUNCH" },
            new { foodId = Guid.NewGuid(), mealType = "DINNER" }
        }), weightKg: 74);

        Assert.True(result.IsError);
        using var json = JsonDocument.Parse(Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        var receipt = json.RootElement;
        Assert.Equal(1, receipt.GetProperty("completed").GetArrayLength());
        Assert.Equal(0, receipt.GetProperty("completed")[0].GetProperty("mealIndex").GetInt32());
        Assert.False(receipt.GetProperty("failed").GetProperty("mayHaveBeenSaved").GetBoolean());
        Assert.Equal(3, receipt.GetProperty("remaining").GetArrayLength());
        Assert.Equal(1, receipt.GetProperty("remaining")[0].GetProperty("mealIndex").GetInt32());
        api.Verify(client => client.PostAsync("/api/Meals", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        api.Verify(client => client.PostAsync("/api/BodyMeasurements", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LogDayMarksAServerFailureUnconfirmedInsteadOfInvitingADuplicateWrite()
    {
        var api = new Mock<IBackendApiClient>();
        api.Setup(client => client.PostAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BackendApiException(HttpStatusCode.InternalServerError, null, "Connection failed after save"));
        var result = await new MealTools(api.Object).LogDay("2026-09-05", weightKg: 74);
        using var json = JsonDocument.Parse(Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        Assert.True(json.RootElement.GetProperty("failed").GetProperty("mayHaveBeenSaved").GetBoolean());
        Assert.Equal(0, json.RootElement.GetProperty("remaining").GetArrayLength());
    }

    [Fact]
    public async Task PromotionCarriesExplicitWeightsAndReturnsMissingWeightDetailsToTheAgent()
    {
        var api = new Mock<IBackendApiClient>();
        var entryId = Guid.NewGuid();
        var body = JsonSerializer.Serialize(new { errorCode = "promotion_weights_required", items = new[] { new { id = entryId, name = "Rice", kind = "entry" } } });
        api.Setup(client => client.PostAsync("/api/Recipes/promote", It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BackendApiException(HttpStatusCode.BadRequest, "promotion_weights_required", "Need weight", body));
        var tools = new RecipeTools(api.Object);
        var result = await tools.PromoteToRecipe("2026-09-05", "LUNCH", "Chicken and rice");
        Assert.True(result.IsError);
        Assert.Equal(body, Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);

        api.Setup(client => client.PostAsync("/api/Recipes/promote", It.IsAny<object>(), It.IsAny<CancellationToken>())).ReturnsAsync("{\"recipeId\":\"saved\"}");
        await tools.PromoteToRecipe("2026-09-05", "LUNCH", "Chicken and rice",
            entryWeightsGramsJson: JsonSerializer.Serialize(new Dictionary<Guid, decimal> { [entryId] = 150 }));
        api.Verify(client => client.PostAsync("/api/Recipes/promote", It.Is<PromoteMealToRecipeRequest>(request => request.EntryWeightsGrams != null && request.EntryWeightsGrams.ContainsKey(entryId) && request.EntryWeightsGrams[entryId] == 150), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ToolCatalogueExposesPromotionInsteadOfStandaloneAuthoring()
    {
        var names = typeof(RecipeTools).GetMethods().Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name).ToArray();
        Assert.Contains("promote_to_recipe", names);
        Assert.Contains("promote_recipe_to_preparation", names);
        Assert.DoesNotContain("create_recipe", names);
        Assert.Contains(typeof(MealTools).GetMethods(), method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name == "log_day");
    }
}
