using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Mizan.Tests.Integration;

namespace Mizan.Tests.Eval;

/// <summary>The public foods every run shares, with their nutrition per 100 g and serving size.</summary>
public sealed record EvalFood(string Name, decimal Calories, decimal Protein, decimal Carbs, decimal Fat, decimal Serving);

/// <summary>
/// One run's people and history: a person with two weeks of logs, a coach with that person as a client and a
/// pending request, and an administrator. Every run seeds its own, so runs do not see each other's writes and
/// can go side by side. The catalogue foods are shared and seeded once.
/// </summary>
public sealed class EvalWorld
{
    public static readonly EvalFood[] Foods =
    [
        new("Greek yogurt, plain", 97, 9, 3.9m, 5, 170),
        new("Banana", 89, 1.1m, 22.8m, 0.3m, 118),
        new("Peanut butter", 588, 25, 20, 50, 32),
        new("Rolled oats", 379, 13, 68, 6.5m, 40),
        new("Chicken breast, grilled", 165, 31, 0, 3.6m, 100),
        new("White rice, cooked", 130, 2.7m, 28, 0.3m, 158),
        new("Broccoli, steamed", 35, 2.4m, 7.2m, 0.4m, 91),
        new("Whole wheat bread", 247, 13, 41, 3.4m, 32),
        new("Salmon fillet", 208, 20, 0, 13, 150),
        new("Egg, boiled", 155, 13, 1.1m, 11, 50),
        new("Milk, whole", 61, 3.2m, 4.8m, 3.3m, 244),
        new("Spinach, raw", 23, 2.9m, 3.6m, 0.4m, 30),
    ];

    /// <summary>What a description of a meal is estimated as. The provider is a fake; the estimate is the same for both arms.</summary>
    public const string SoupEstimate = """
        {"foods":[
           {"name":"Lentil soup","portionGrams":350,"calories":300,"protein":18,"carbs":42,"fat":6},
           {"name":"Crusty bread","portionGrams":40,"calories":100,"protein":3,"carbs":19,"fat":1}],
         "totalCalories":400,"confidence":0.6,"note":"Assumed a 350 g bowl and one 40 g slice."}
        """;

    public required string Tag { get; init; }
    public required DateOnly Today { get; init; }
    public DateOnly Yesterday => Today.AddDays(-1);

    public required Guid User { get; init; }
    public required string UserEmail { get; init; }
    public required Guid Friend { get; init; }
    public required Guid Trainer { get; init; }
    public required Guid Requester { get; init; }
    public required Guid Admin { get; init; }

    public required Guid BowlRecipe { get; init; }
    public required Guid OatsRecipe { get; init; }
    public required Guid GroceryList { get; init; }
    public required Guid Plan { get; init; }
    public required Guid Achievement { get; init; }
    public required string AchievementName { get; init; }

    /// <summary>Today's seeded breakfast, and what is left of the goal after it.</summary>
    public required decimal CaloriesLeftToday { get; init; }
    public required decimal ProteinLeftToday { get; init; }

    /// <summary>The protein the person logged yesterday, which the coach is asked about.</summary>
    public required decimal ProteinYesterday { get; init; }

    public static async Task SeedCatalogueAsync(ApiTestFixture fixture)
    {
        await WithDbAsync(fixture, async db =>
        {
            var now = DateTime.UtcNow;
            foreach (var food in Foods)
            {
                db.Foods.Add(new Food
                {
                    Id = Guid.NewGuid(),
                    Name = food.Name,
                    CaloriesPer100g = food.Calories,
                    ProteinPer100g = food.Protein,
                    CarbsPer100g = food.Carbs,
                    FatPer100g = food.Fat,
                    ServingSize = food.Serving,
                    ServingUnit = "g",
                    IsVerified = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }
            await db.SaveChangesAsync();
        });
    }

    public static async Task<EvalWorld> SeedAsync(ApiTestFixture fixture, int index)
    {
        var tag = $"{index:D3}{Guid.NewGuid().ToString("N")[..4]}";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var user = Guid.NewGuid();
        var friend = Guid.NewGuid();
        var trainer = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var email = $"sara-{tag}@eval.test";

        await Person(fixture, user, email, "Sara Kebede", "user");
        await Person(fixture, friend, $"dawit-{tag}@eval.test", "Dawit Alemu", "user");
        await Person(fixture, trainer, $"hana-{tag}@eval.test", "Hana Tesfaye", "trainer");
        await Person(fixture, requester, $"omar-{tag}@eval.test", "Omar Said", "user");
        await Person(fixture, admin, $"ruth-{tag}@eval.test", "Ruth Admin", "admin");
        await fixture.GrantAiConsentAsync(user);

        var bowl = Guid.NewGuid();
        var oats = Guid.NewGuid();
        var list = Guid.NewGuid();
        var achievement = Guid.NewGuid();
        var achievementName = $"Spring sprint {tag}";
        decimal caloriesToday = 0, proteinToday = 0, proteinYesterday = 0;

        await WithDbAsync(fixture, async db =>
        {
            var now = DateTime.UtcNow;
            var foods = await db.Foods.Where(f => f.UserId == null).ToDictionaryAsync(f => f.Name);
            var exercises = await db.Exercises.Where(e => !e.IsCustom).ToDictionaryAsync(e => e.Name);

            db.UserGoals.Add(new UserGoal
            {
                Id = Guid.NewGuid(), UserId = user, GoalType = "maintenance", TargetCalories = 2000, TargetProteinGrams = 130,
                TargetCarbsGrams = 220, TargetFatGrams = 65, IsActive = true, CreatedAt = now.AddDays(-20), UpdatedAt = now.AddDays(-20),
            });

            FoodDiaryEntry Entry(DateOnly date, string meal, string food, decimal grams)
            {
                var f = foods[food];
                return new FoodDiaryEntry
                {
                    Id = Guid.NewGuid(), UserId = user, FoodId = f.Id, Name = f.Name, EntryDate = date, MealType = meal,
                    AmountGrams = grams, Servings = Math.Round(grams / f.ServingSize, 2),
                    Calories = Math.Round(f.CaloriesPer100g * grams / 100, 2), ProteinGrams = Math.Round(f.ProteinPer100g * grams / 100, 2),
                    CarbsGrams = Math.Round(f.CarbsPer100g * grams / 100, 2), FatGrams = Math.Round(f.FatPer100g * grams / 100, 2),
                    LoggedAt = date.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc), UpdatedAt = now,
                };
            }

            var breakfast = new[]
            {
                Entry(today, "BREAKFAST", "Rolled oats", 80),
                Entry(today, "BREAKFAST", "Banana", 120),
                Entry(today, "BREAKFAST", "Peanut butter", 16),
            };
            var yesterday = new[]
            {
                Entry(today.AddDays(-1), "BREAKFAST", "Greek yogurt, plain", 170),
                Entry(today.AddDays(-1), "LUNCH", "Chicken breast, grilled", 200),
                Entry(today.AddDays(-1), "LUNCH", "White rice, cooked", 200),
                Entry(today.AddDays(-1), "DINNER", "Salmon fillet", 150),
            };
            db.FoodDiaryEntries.AddRange(breakfast);
            db.FoodDiaryEntries.AddRange(yesterday);
            caloriesToday = breakfast.Sum(e => e.Calories ?? 0);
            proteinToday = breakfast.Sum(e => e.ProteinGrams ?? 0);
            proteinYesterday = yesterday.Sum(e => e.ProteinGrams ?? 0);

            RecipeIngredient Ingredient(string food, decimal grams, int order) => new()
            {
                Id = Guid.NewGuid(), FoodId = foods[food].Id, IngredientText = $"{grams:0} g {food}", Amount = grams, Unit = "g", SortOrder = order,
            };
            db.Recipes.Add(new Recipe
            {
                Id = bowl, UserId = user, Title = "Chicken rice bowl", Servings = 2, PrepTimeMinutes = 25, CreatedAt = now.AddDays(-30), UpdatedAt = now.AddDays(-30),
                Ingredients = [Ingredient("Chicken breast, grilled", 300, 0), Ingredient("White rice, cooked", 400, 1), Ingredient("Broccoli, steamed", 200, 2)],
            });
            db.Recipes.Add(new Recipe
            {
                Id = oats, UserId = user, Title = "Overnight oats", Servings = 1, PrepTimeMinutes = 5, CreatedAt = now.AddDays(-30), UpdatedAt = now.AddDays(-30),
                Ingredients = [Ingredient("Rolled oats", 60, 0), Ingredient("Milk, whole", 200, 1), Ingredient("Banana", 60, 2)],
            });

            for (var k = 0; k < 14; k++)
            {
                db.BodyMeasurements.Add(new BodyMeasurement
                {
                    Id = Guid.NewGuid(), UserId = user, MeasurementDate = today.AddDays(-14 + k), WeightKg = 82.0m - 0.1m * k,
                    CreatedAt = now.AddDays(-14 + k), UpdatedAt = now.AddDays(-14 + k),
                });
            }

            var bench = exercises["Bench Press"].Id;
            var row = exercises["Barbell Row"].Id;
            var template = new WorkoutTemplate
            {
                Id = Guid.NewGuid(), UserId = user, Name = "Upper A", SessionOrder = 0, SortOrder = 0, CreatedAt = now.AddDays(-20), UpdatedAt = now.AddDays(-20),
                Exercises =
                [
                    new WorkoutTemplateExercise { Id = Guid.NewGuid(), ExerciseId = bench, SortOrder = 0, Sets = 3, RepsPerSet = 5, TargetWeightKg = 62.5m },
                    new WorkoutTemplateExercise { Id = Guid.NewGuid(), ExerciseId = row, SortOrder = 1, Sets = 3, RepsPerSet = 8, TargetWeightKg = 50m },
                ],
            };
            db.WorkoutTemplates.Add(template);
            foreach (var (daysAgo, benchKg, rowKg) in new[] { (6, 57.5m, 45m), (3, 60m, 47.5m) })
            {
                var date = today.AddDays(-daysAgo);
                db.Workouts.Add(new Workout
                {
                    Id = Guid.NewGuid(), UserId = user, Name = "Upper A", WorkoutDate = date, TemplateId = template.Id, DurationMinutes = 55,
                    StartedAt = date.ToDateTime(new TimeOnly(18, 0), DateTimeKind.Utc), CompletedAt = date.ToDateTime(new TimeOnly(18, 55), DateTimeKind.Utc),
                    CreatedAt = now.AddDays(-daysAgo), UpdatedAt = now.AddDays(-daysAgo),
                    Exercises =
                    [
                        new WorkoutExercise { Id = Guid.NewGuid(), ExerciseId = bench, SortOrder = 0, Sets = Sets(3, 5, benchKg) },
                        new WorkoutExercise { Id = Guid.NewGuid(), ExerciseId = row, SortOrder = 1, Sets = Sets(3, 8, rowKg) },
                    ],
                });
            }

            db.ShoppingLists.Add(new ShoppingList
            {
                Id = list, UserId = user, Name = "Weekly groceries", CreatedAt = now.AddDays(-2), UpdatedAt = now.AddDays(-2),
                Items =
                [
                    new ShoppingListItem { Id = Guid.NewGuid(), ItemName = "Bananas", Amount = 6, Unit = "pcs", SortOrder = 0, FoodId = foods["Banana"].Id },
                    new ShoppingListItem { Id = Guid.NewGuid(), ItemName = "Rolled oats", Amount = 1, Unit = "kg", SortOrder = 1, FoodId = foods["Rolled oats"].Id },
                    new ShoppingListItem { Id = Guid.NewGuid(), ItemName = "Chicken breast", Amount = 1, Unit = "kg", SortOrder = 2 },
                ],
            });

            db.SocialProfiles.Add(new SocialProfile { UserId = friend, DisplayName = "Dawit A", ShareToken = Guid.NewGuid().ToString("N"), CreatedAt = now, UpdatedAt = now });
            db.Follows.Add(new Follow { Id = Guid.NewGuid(), FollowerUserId = friend, FolloweeUserId = user, Status = "Pending", CreatedAt = now.AddHours(-3) });

            // An accepted request opens the conversation, as RespondToTrainerRequestCommand does.
            var coaching = Guid.NewGuid();
            db.TrainerClientRelationships.Add(new TrainerClientRelationship
            {
                Id = coaching, TrainerId = trainer, ClientId = user, Status = "active", CanViewNutrition = true, CanViewWorkouts = true,
                CanViewMeasurements = true, CanMessage = true, StartedAt = now.AddDays(-40), CreatedAt = now.AddDays(-40),
            });
            db.ChatConversations.Add(new ChatConversation { Id = Guid.NewGuid(), TrainerClientRelationshipId = coaching, CreatedAt = now.AddDays(-40), UpdatedAt = now.AddDays(-40) });
            db.TrainerClientRelationships.Add(new TrainerClientRelationship
            {
                Id = Guid.NewGuid(), TrainerId = trainer, ClientId = requester, Status = "pending", CanViewNutrition = true, CanMessage = true, CreatedAt = now.AddHours(-5),
            });

            db.Achievements.Add(new Achievement
            {
                Id = achievement, Name = achievementName, Description = "Created by mistake during a spring campaign.", Points = 10, Category = "nutrition",
                CriteriaType = "streak_days", Threshold = 3,
            });

            await db.SaveChangesAsync();
        });

        // The active meal plan is made the way a person makes one, so it holds what the commands require.
        var plan = await ActivePlanAsync(fixture, user, email, today, bowl, oats);

        return new EvalWorld
        {
            Tag = tag,
            Today = today,
            User = user,
            UserEmail = email,
            Friend = friend,
            Trainer = trainer,
            Requester = requester,
            Admin = admin,
            BowlRecipe = bowl,
            OatsRecipe = oats,
            GroceryList = list,
            Plan = plan,
            Achievement = achievement,
            AchievementName = achievementName,
            CaloriesLeftToday = 2000 - caloriesToday,
            ProteinLeftToday = 130 - proteinToday,
            ProteinYesterday = proteinYesterday,
        };
    }

    private static List<ExerciseSet> Sets(int count, int reps, decimal kg) => Enumerable.Range(1, count)
        .Select(n => new ExerciseSet { Id = Guid.NewGuid(), SetNumber = n, Reps = reps, WeightKg = kg, Completed = true })
        .ToList();

    private static async Task<Guid> ActivePlanAsync(ApiTestFixture fixture, Guid user, string email, DateOnly today, Guid bowl, Guid oats)
    {
        Guid salmon = Guid.Empty;
        await WithDbAsync(fixture, async db => salmon = await db.Foods.Where(f => f.UserId == null && f.Name == "Salmon fillet").Select(f => f.Id).SingleAsync());
        using var browser = fixture.CreateAuthenticatedClient(user, email);
        var response = await browser.PostAsJsonAsync("/api/tools/plan_meals/call", new
        {
            surface = "mcp",
            arguments = new
            {
                action = "create",
                name = "This week",
                startDate = today.ToString("yyyy-MM-dd"),
                durationDays = 7,
                entries = new object[]
                {
                    new { dayIndex = 0, slot = "DINNER", kind = "Food", foodId = salmon, grams = 150 },
                    new { dayIndex = 1, slot = "DINNER", kind = "Recipe", recipeId = bowl, servings = 1 },
                    new { dayIndex = 2, slot = "BREAKFAST", kind = "Recipe", recipeId = oats, servings = 1 },
                    new { dayIndex = 2, slot = "LUNCH", kind = "Recipe", recipeId = bowl, servings = 1 },
                },
            },
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (!response.IsSuccessStatusCode || body.GetProperty("isError").GetBoolean())
        {
            throw new InvalidOperationException("Seeding the meal plan failed: " + body);
        }

        Guid plan = Guid.Empty;
        await WithDbAsync(fixture, async db => plan = await db.MealPlans.Where(p => p.UserId == user && p.Name == "This week").Select(p => p.Id).SingleAsync());
        return plan;
    }

    private static async Task Person(ApiTestFixture fixture, Guid id, string email, string name, string role)
    {
        await fixture.SeedUserAsync(id, email, role: role);
        await fixture.GrantProAsync(id);
        await WithDbAsync(fixture, async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == id);
            user.Name = name;
            user.TimeZoneId = "UTC";
            await db.SaveChangesAsync();
        });
    }

    public static async Task WithDbAsync(ApiTestFixture fixture, Func<MizanDbContext, Task> action)
    {
        using var scope = fixture.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<MizanDbContext>());
    }

    public static async Task<T> QueryAsync<T>(ApiTestFixture fixture, Func<MizanDbContext, Task<T>> query)
    {
        using var scope = fixture.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<MizanDbContext>());
    }
}
