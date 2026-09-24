using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Common;
using Mizan.Application.Queries;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Mizan.Infrastructure.Data.Legacy;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// Issue #85: imported recipes whose ingredients cannot be summed show and log
/// the nutrition v1 kept for them - through the exact import script an
/// operator runs - and only while that nutrition still describes the recipe.
/// </summary>
[Collection("ApiIntegration")]
public class RetainedRecipeNutritionTests(ApiTestFixture fixture)
{
    private const string LegacySchema = """
        DROP SCHEMA IF EXISTS legacy_v1 CASCADE;
        CREATE SCHEMA legacy_v1;
        CREATE TABLE legacy_v1.recipes (id uuid PRIMARY KEY, servings integer NOT NULL);
        CREATE TABLE legacy_v1.recipe_ingredients (
            id uuid PRIMARY KEY, recipe_id uuid NOT NULL, food_id uuid, ingredient_text varchar(255) NOT NULL,
            amount numeric(10,2), unit varchar(50), sort_order integer NOT NULL DEFAULT 0, sub_recipe_id uuid);
        CREATE TABLE legacy_v1.recipe_nutrition (
            recipe_id uuid NOT NULL, calories_per_serving numeric(8,2), protein_grams numeric(8,2), carbs_grams numeric(8,2),
            fat_grams numeric(8,2), fiber_grams numeric(8,2), sugar_grams numeric(8,2), sodium_mg numeric(8,2), protein_calorie_ratio numeric(8,2));
        """;

    [Fact]
    public async Task RetainedNutrition_IsImportedOnlyWhenCurrentAndPlausible_AndRetiresOnEdit()
    {
        await fixture.ResetDatabaseAsync();
        var userId = Guid.NewGuid();
        var email = $"retained-{userId:N}@example.com";
        await fixture.SeedUserAsync(userId, email);
        var chicken = await fixture.SeedFoodAsync("Chicken Breast", 165m, 31m, 0m, 3.6m);
        using var client = fixture.CreateAuthenticatedClient(userId, email);

        // Each recipe has a measured line with no food, so none can be summed.
        var current = await SeedImportedRecipeAsync(userId, "Papaya Chicken", chicken.Id);
        var editedSinceV1 = await SeedImportedRecipeAsync(userId, "Edited Since", chicken.Id);
        var implausible = await SeedImportedRecipeAsync(userId, "Nonsense", chicken.Id);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
#pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync(LegacySchema);
            foreach (var recipe in new[] { current, editedSinceV1, implausible })
            {
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO legacy_v1.recipes (id, servings) VALUES ({0}, 2)", recipe);
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO legacy_v1.recipe_ingredients (id, recipe_id, food_id, ingredient_text, amount, unit, sort_order)
                    SELECT id, recipe_id, food_id, ingredient_text, amount, unit, sort_order FROM public.recipe_ingredients WHERE recipe_id = {0}
                    """, recipe);
            }

            // 4*40 + 4*30 + 9*20 = 460 kcal: consistent.
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO legacy_v1.recipe_nutrition (recipe_id, calories_per_serving, protein_grams, carbs_grams, fat_grams, fiber_grams) VALUES ({0}, 460, 40, 30, 20, 4.5)", current);
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO legacy_v1.recipe_nutrition (recipe_id, calories_per_serving, protein_grams, carbs_grams, fat_grams, fiber_grams) VALUES ({0}, 460, 40, 30, 20, 4.5)", editedSinceV1);
            // 900 kcal stated against 460 kcal of macros: a broken import.
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO legacy_v1.recipe_nutrition (recipe_id, calories_per_serving, protein_grams, carbs_grams, fat_grams) VALUES ({0}, 900, 40, 30, 20)", implausible);

            // The v1 lines of this one differ from today's: the value describes another recipe.
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE legacy_v1.recipe_ingredients SET amount = 500 WHERE recipe_id = {0} AND food_id IS NOT NULL", editedSinceV1);

            await db.Database.ExecuteSqlRawAsync(LegacyRecipeNutritionImport.Sql);
            await db.Database.ExecuteSqlRawAsync(LegacyRecipeNutritionImport.Sql); // idempotent
#pragma warning restore EF1002

            (await db.RecipeNutritionSnapshots.Select(s => s.RecipeId).ToListAsync()).Should().Equal(current);
        }

        var detail = await client.GetFromJsonAsync<RecipeDetailDto>($"/api/Recipes/{current}");
        detail!.NutritionSource.Should().Be(RecipeNutritionSources.Retained);
        detail.Nutrition!.CaloriesPerServing.Should().Be(460);
        detail.UnresolvedIngredients.Should().Equal("600g papaya");
        (await client.GetFromJsonAsync<RecipeDetailDto>($"/api/Recipes/{editedSinceV1}"))!.Nutrition.Should().BeNull();

        // Logged as shown: one row, scaled by servings, no invented ingredient rows.
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        (await client.PostAsJsonAsync("/api/Nutrition/log", new { recipeId = current, entryDate = day, mealType = "DINNER", servings = 1.5 }))
            .IsSuccessStatusCode.Should().BeTrue();
        var daily = await client.GetFromJsonAsync<DailyNutritionResult>($"/api/Nutrition/daily?date={day:yyyy-MM-dd}");
        daily!.TotalCalories.Should().Be(690);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var rows = await scope.ServiceProvider.GetRequiredService<MizanDbContext>().FoodDiaryEntries
                .Where(e => e.RecipeId == current).ToListAsync();
            rows.Should().ContainSingle().Which.FoodId.Should().BeNull();
        }

        // Changing the serving count means the per-serving value no longer fits.
        (await client.PutAsJsonAsync($"/api/Recipes/{current}", new
        {
            id = current, title = "Papaya Chicken", servings = 3, isPublic = false,
            ingredients = detail.Ingredients.Select(i => new { foodId = i.FoodId, ingredientText = i.IngredientText, amount = i.Amount, unit = i.Unit })
        })).EnsureSuccessStatusCode();

        detail = await client.GetFromJsonAsync<RecipeDetailDto>($"/api/Recipes/{current}");
        detail!.Nutrition.Should().BeNull();
        detail.NutritionSource.Should().BeNull();
        var refused = await client.PostAsJsonAsync("/api/Nutrition/log", new { recipeId = current, entryDate = day, mealType = "DINNER", servings = 1 });
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("600g papaya");

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
#pragma warning disable EF1002
            await scope.ServiceProvider.GetRequiredService<MizanDbContext>().Database.ExecuteSqlRawAsync("DROP SCHEMA legacy_v1 CASCADE");
#pragma warning restore EF1002
        }
    }

    private async Task<Guid> SeedImportedRecipeAsync(Guid userId, string title, Guid chickenId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var now = DateTime.UtcNow;
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(), UserId = userId, Title = title, Servings = 2, CreatedAt = now, UpdatedAt = now,
            Ingredients =
            {
                new RecipeIngredient { Id = Guid.NewGuid(), FoodId = chickenId, IngredientText = "400g chicken", Amount = 400, Unit = "g", SortOrder = 0 },
                new RecipeIngredient { Id = Guid.NewGuid(), IngredientText = "600g papaya", Amount = 600, Unit = "g", SortOrder = 1 },
                new RecipeIngredient { Id = Guid.NewGuid(), IngredientText = "salt, pepper, paprika", SortOrder = 2 }
            }
        };
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        return recipe.Id;
    }
}
