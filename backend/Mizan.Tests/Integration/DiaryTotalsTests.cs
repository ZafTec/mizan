using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Queries;
using Mizan.Contracts.Meals;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The diary and the meal form show numbers the server computed. The browser adds and multiplies
/// nothing (CLAUDE.md), so the subtotals and the form's running total come from the API.
/// </summary>
[Collection("ApiIntegration")]
public class DiaryTotalsTests
{
    private readonly ApiTestFixture _fixture;
    private readonly PlanningWorld _world;

    public DiaryTotalsTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _world = new PlanningWorld(fixture);
    }

    [Fact]
    public async Task TheDiaryAddsUpEachMealAndEachLoggedRecipe_SoNoScreenDoes()
    {
        await _fixture.ResetDatabaseAsync();
        var (me, client) = await _world.PersonAsync("diary");
        var rice = await _world.FoodAsync("Rice", 130, 2.7m, 28, 0.3m);
        var chicken = await _world.FoodAsync("Chicken", 165, 31, 0, 3.6m);
        var chickenRice = await _world.RecipeAsync(me, "Chicken rice", 2, false, (rice, 200, "g", null), (chicken, 300, "g", null));
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        (await client.PostAsJsonAsync("/api/Nutrition/log", new { recipeId = chickenRice.Id, entryDate = today, mealType = "LUNCH", servings = 1 }))
            .EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/Meals", new { name = "Tea", entryDate = today, mealType = "lunch", servings = 1, calories = 30, proteinGrams = 1 }))
            .EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/Meals", new { name = "Soup", entryDate = today, mealType = "DINNER", servings = 1, calories = 210.4 }))
            .EnsureSuccessStatusCode();

        var diary = (await client.GetFromJsonAsync<FoodDiaryResult>($"/api/Meals?date={today}"))!;

        // One serving is half of 200 g rice and 300 g chicken: 130 + 247.5 kcal, logged as one recipe group.
        diary.Groups.Should().ContainSingle().Which.Totals.Calories.Should().Be(377.5m);
        diary.Meals.Select(m => (m.MealType, m.Totals.Calories)).Should().Equal(("LUNCH", 407.5m), ("DINNER", 210.4m));
        diary.Meals[0].Totals.Protein.Should().Be(50.2m, "2.7 + 46.5 from the recipe and 1 from the tea");
    }

    [Fact]
    public async Task TheMealFormPreview_ComesToWhatLoggingStores()
    {
        await _fixture.ResetDatabaseAsync();
        var (me, client) = await _world.PersonAsync("preview");
        var (someoneElse, _) = await _world.PersonAsync("other");
        var rice = await _world.FoodAsync("Rice", 130, 2.7m, 28, 0.3m);
        var chicken = await _world.FoodAsync("Chicken", 165, 31, 0, 3.6m);
        var chickenRice = await _world.RecipeAsync(me, "Chicken rice", 2, false, (rice, 200, "g", null), (chicken, 300, "g", null));
        var secret = await _world.RecipeAsync(someoneElse, "Secret stew", 1, false, (rice, 100, "g", null));
        var estimate = new NutritionPer100g { Calories = 165.33m, ProteinGrams = 31, CarbsGrams = 0.1m, FatGrams = 3.6m };

        var response = await client.PostAsJsonAsync("/api/Meals/preview", new MealPreviewRequest
        {
            Lines =
            [
                new MealPreviewLine { Key = "rice", FoodId = rice.Id, Grams = 150 },
                new MealPreviewLine { Key = "bowl", RecipeId = chickenRice.Id, Servings = 1.5m },
                new MealPreviewLine { Key = "photo", Per100g = estimate, Grams = 180 },
                new MealPreviewLine { Key = "tea", Calories = 30, ProteinGrams = 1 },
                new MealPreviewLine { Key = "secret", RecipeId = secret.Id, Servings = 1 },
            ],
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var preview = (await response.Content.ReadFromJsonAsync<MealPreviewResult>())!;
        preview.Lines.Select(l => (l.Key, l.Calories)).Should().Equal(
            ("rice", 195m), ("bowl", 566.25m), ("photo", 297.59m), ("tea", 30m), ("secret", (decimal?)null));
        preview.Calories.Should().Be(1088.8m, "195 + 566.25 + 297.59 + 30, rounded to a tenth");
        preview.Complete.Should().BeFalse("another person's private recipe previews as unknown");

        // Logging the estimate stores exactly what the preview showed for it.
        (await client.PostAsJsonAsync("/api/Meals", new
        {
            name = "Grilled chicken",
            mealType = "LUNCH",
            servings = 1,
            amountGrams = 180,
            per100g = estimate,
        })).EnsureSuccessStatusCode();
        (await _world.DbAsync(db => db.FoodDiaryEntries.Where(e => e.UserId == me).Select(e => e.Calories).SingleAsync()))
            .Should().Be(297.59m);
    }
}
