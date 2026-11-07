using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Mizan.Infrastructure.Data;
using Mizan.Tests.Integration;

namespace Mizan.Tests.Eval;

public enum EvalActor
{
    User,
    Trainer,
    Admin,
}

/// <summary>What a check can read: the database after the conversation, the run's world, and everything the model said.</summary>
public sealed record EvalContext(ApiTestFixture Fixture, EvalWorld World, string Answer)
{
    public Task<T> Db<T>(Func<MizanDbContext, Task<T>> query) => EvalWorld.QueryAsync(Fixture, query);
}

/// <summary>
/// One request. <see cref="Legacy"/> and <see cref="Tasks"/> name what can serve it in each toolset: a tool, or for
/// the task tools a tool and its mode (<c>tool:mode</c>). A call to anything else is a wrong call. The check returns
/// null when the request was done, or what is missing.
/// </summary>
public sealed record EvalCase(
    string Id,
    string Group,
    EvalActor Actor,
    Func<EvalWorld, string> Prompt,
    string[] Legacy,
    string[] Tasks,
    Func<EvalContext, Task<string?>> Check,
    string? FollowUp = null);

public static class EvalCases
{
    private const string GoAhead = "Yes, go ahead and do it. No need to ask me anything else.";

    public static IReadOnlyList<EvalCase> All { get; } =
    [
        // Food logging
        new("food-yogurt", "food", EvalActor.User,
            _ => "Log 250 g of plain Greek yogurt for breakfast today.",
            ["search_foods", "get_food", "log_food", "log_day", "get_food_diary"],
            ["find_food:food", "log_food", "get_nutrition:day"],
            c => Calories(c, c.World.Today, "BREAKFAST", "Greek yogurt, plain", 242.5m, 0.03m)),
        new("food-snack-two", "food", EvalActor.User,
            _ => "Snack just now: a banana and 40 g of peanut butter. Please log it.",
            ["search_foods", "get_food", "log_food", "log_day", "get_food_diary"],
            ["find_food:food", "log_food", "get_nutrition:day"],
            async c => await Calories(c, c.World.Today, "SNACK", "Peanut butter", 235.2m, 0.05m)
                ?? await Range(c, c.World.Today, "SNACK", "Banana", 70, 150)),
        new("food-lunch-three", "food", EvalActor.User,
            _ => "For lunch today I had 150 g grilled chicken breast, 200 g cooked white rice and 100 g steamed broccoli.",
            ["search_foods", "get_food", "log_food", "log_day", "get_food_diary"],
            ["find_food:food", "log_food", "get_nutrition:day"],
            async c => await Calories(c, c.World.Today, "LUNCH", "Chicken breast, grilled", 247.5m, 0.05m)
                ?? await Calories(c, c.World.Today, "LUNCH", "White rice, cooked", 260m, 0.05m)
                ?? await Calories(c, c.World.Today, "LUNCH", "Broccoli, steamed", 35m, 0.08m)),
        new("food-recipe-dinner", "food", EvalActor.User,
            _ => "Log one serving of my Chicken rice bowl recipe for dinner tonight.",
            ["search_recipes", "get_recipe", "log_meal", "log_day", "get_food_diary"],
            ["find_food:recipe", "log_food", "get_nutrition:day"],
            c => RecipeCalories(c, c.World.Today, "DINNER", c.World.BowlRecipe, 542.5m)),
        new("food-recipe-yesterday", "food", EvalActor.User,
            _ => "I forgot to log yesterday's breakfast: two servings of my overnight oats.",
            ["search_recipes", "get_recipe", "log_meal", "log_day", "get_food_diary"],
            ["find_food:recipe", "log_food", "get_nutrition:day"],
            c => RecipeCalories(c, c.World.Yesterday, "BREAKFAST", c.World.OatsRecipe, 805.6m)),
        new("food-estimate", "food", EvalActor.User,
            _ => "For lunch I had a bowl of homemade lentil soup and a slice of crusty bread. I didn't weigh anything, so estimate it and log it.",
            ["analyze_food_text", "log_meal_manual", "log_day", "log_food", "search_foods", "get_food_diary"],
            ["estimate_food:text", "log_food", "find_food:food", "get_nutrition:day"],
            c => MealTotal(c, c.World.Today, "LUNCH", 360, 440)),
        new("food-known-nutrition", "food", EvalActor.User,
            _ => "Log a protein bar as a snack: 210 kcal, 20 g protein, 22 g carbs, 7 g fat.",
            ["log_meal_manual", "log_day", "get_food_diary"],
            ["log_food", "get_nutrition:day"],
            async c =>
            {
                var today = c.World.Today;
                var rows = await c.Db(db => db.FoodDiaryEntries.Where(e => e.UserId == c.World.User && e.EntryDate == today && e.MealType == "SNACK").ToListAsync());
                return rows.Any(r => Near(r.Calories, 210, 3) && Near(r.ProteinGrams, 20, 1) && Near(r.CarbsGrams, 22, 1) && Near(r.FatGrams, 7, 1))
                    ? Once(rows.Count, "a snack row")
                    : "no snack of 210 kcal, 20 g protein, 22 g carbs and 7 g fat";
            }),
        new("food-left-today", "food", EvalActor.User,
            _ => "How many calories and how much protein do I have left for today?",
            ["get_daily_nutrition", "get_nutrition_summary", "get_current_goal", "get_food_diary", "get_goal_progress"],
            ["get_nutrition:day", "get_nutrition:goals"],
            c => Task.FromResult(Mentions(c.Answer, c.World.CaloriesLeftToday, 15, "calories left")
                ?? Mentions(c.Answer, c.World.ProteinLeftToday, 2, "protein left"))),
        new("food-delete", "food", EvalActor.User,
            _ => "Remove the banana from this morning's breakfast, I didn't eat it after all.",
            ["get_food_diary", "delete_meal"],
            ["get_nutrition:day", "delete:diary_entry"],
            async c =>
            {
                var today = c.World.Today;
                var names = await c.Db(db => db.FoodDiaryEntries.Where(e => e.UserId == c.World.User && e.EntryDate == today && e.MealType == "BREAKFAST").Select(e => e.Name).ToListAsync());
                if (names.Contains("Banana")) return "the banana is still logged";
                return names.Count == 2 ? null : $"breakfast has {names.Count} rows, expected the oats and the peanut butter";
            }),
        new("food-save-recipe", "food", EvalActor.User,
            _ => "Save today's breakfast as a recipe called Power oats.",
            ["get_food_diary", "promote_to_recipe", "search_recipes"],
            ["get_nutrition:day", "save_food:recipe", "find_food:recipe"],
            async c =>
            {
                var recipe = await c.Db(db => db.Recipes.Include(r => r.Ingredients)
                    .Where(r => r.UserId == c.World.User && r.Title.ToLower().Contains("power oats")).ToListAsync());
                if (recipe.Count != 1) return $"{recipe.Count} recipes called Power oats";
                return recipe[0].Ingredients.Count == 3 ? null : $"Power oats has {recipe[0].Ingredients.Count} ingredients, expected 3";
            }),

        // Planning and shopping
        new("plan-weekday-lunches", "planning", EvalActor.User,
            w => $"Make me a new meal plan for next week, starting Monday {NextMonday(w.Today):yyyy-MM-dd}, with my Chicken rice bowl (one serving) for lunch every weekday.",
            ["create_meal_plan", "add_plan_entry", "search_recipes", "get_recipe", "get_meal_plan", "list_meal_plans", "update_meal_plan", "set_meal_plan_status"],
            ["plan_meals:create", "edit_meal_plan", "find_food:recipe", "get_meal_plan:list", "get_meal_plan:plan"],
            async c =>
            {
                var monday = NextMonday(c.World.Today);
                var plans = await c.Db(db => db.MealPlans.Include(p => p.Entries)
                    .Where(p => p.UserId == c.World.User && p.StartDate == monday).ToListAsync());
                if (plans.Count != 1) return $"{plans.Count} plans start on {monday}";
                var days = plans[0].Entries.Where(e => e.Slot == "LUNCH" && e.RecipeId == c.World.BowlRecipe).Select(e => e.DayIndex).ToList();
                return Enumerable.Range(0, 5).All(days.Contains) && days.Count == 5 ? null : $"bowl lunches on days [{string.Join(",", days)}]";
            }),
        new("shop-add-items", "planning", EvalActor.User,
            _ => "Add oat milk, a dozen eggs and spinach to my Weekly groceries list.",
            ["list_shopping_lists", "get_shopping_list", "add_shopping_list_item", "search_foods"],
            ["get_shopping_list:lists", "get_shopping_list:list", "update_shopping_list:add_items", "find_food:food"],
            async c =>
            {
                var items = await c.Db(db => db.ShoppingListItems.Where(i => i.ShoppingListId == c.World.GroceryList).Select(i => i.ItemName.ToLower()).ToListAsync());
                var missing = new[] { "oat milk", "egg", "spinach" }.Where(want => items.Count(i => i.Contains(want)) != 1).ToList();
                return missing.Count == 0 && items.Count == 6 ? null : $"items: {string.Join(", ", items)}";
            }),
        new("shop-tick", "planning", EvalActor.User,
            _ => "I've bought the bananas and the oats, tick them off my Weekly groceries list.",
            ["list_shopping_lists", "get_shopping_list", "toggle_shopping_list_item", "update_shopping_list_item"],
            ["get_shopping_list:lists", "get_shopping_list:list", "update_shopping_list:check", "update_shopping_list:update_item"],
            async c =>
            {
                var items = await c.Db(db => db.ShoppingListItems.Where(i => i.ShoppingListId == c.World.GroceryList).ToDictionaryAsync(i => i.ItemName, i => i.IsChecked));
                return items.GetValueOrDefault("Bananas") && items.GetValueOrDefault("Rolled oats") && !items.GetValueOrDefault("Chicken breast")
                    ? null
                    : string.Join(", ", items.Select(i => $"{i.Key}={i.Value}"));
            }),
        new("shop-from-plan", "planning", EvalActor.User,
            _ => "Make me a shopping list from my current meal plan.",
            ["list_meal_plans", "get_meal_plan", "generate_shopping_list", "get_shopping_list", "list_shopping_lists"],
            ["get_meal_plan:list", "get_meal_plan:plan", "update_shopping_list:from_plan", "get_shopping_list"],
            async c =>
            {
                var lists = await c.Db(db => db.ShoppingLists.Where(l => l.UserId == c.World.User && l.MealPlanId == c.World.Plan).Select(l => l.Items.Count).ToListAsync());
                return lists.Count == 1 && lists[0] > 0 ? null : $"{lists.Count} lists from the plan";
            }),
        new("plan-dinner-tomorrow", "planning", EvalActor.User,
            _ => "What's on my meal plan for dinner tomorrow?",
            ["list_meal_plans", "get_meal_plan", "get_plan_day"],
            ["get_meal_plan:list", "get_meal_plan:plan", "get_meal_plan:day"],
            c => Task.FromResult(c.Answer.Contains("chicken rice bowl", StringComparison.OrdinalIgnoreCase) ? null : "the answer does not name the Chicken rice bowl")),
        new("plan-add-snack", "planning", EvalActor.User,
            _ => "Add 200 g of Greek yogurt as a snack today in my current meal plan.",
            ["list_meal_plans", "get_meal_plan", "get_plan_day", "search_foods", "get_food", "add_plan_entry"],
            ["get_meal_plan:list", "get_meal_plan:plan", "get_meal_plan:day", "find_food:food", "edit_meal_plan"],
            async c =>
            {
                var entries = await c.Db(db => db.PlanEntries.Include(e => e.Food)
                    .Where(e => e.MealPlanId == c.World.Plan && e.DayIndex == 0 && e.Slot == "SNACK").ToListAsync());
                return entries.Count == 1 && entries[0].Food?.Name == "Greek yogurt, plain" && Near(entries[0].Grams, 200, 1)
                    ? null
                    : $"{entries.Count} snack entries on the first day";
            }),

        // Training
        new("train-log", "training", EvalActor.User,
            _ => "Log today's workout: bench press 3 sets of 5 at 62.5 kg, then barbell row 3 sets of 8 at 50 kg.",
            ["list_exercises", "log_workout", "list_workouts", "get_workout"],
            ["get_training:exercises", "log_workout:log", "get_training:history"],
            async c =>
            {
                var today = c.World.Today;
                var sets = await c.Db(db => db.ExerciseSets.Where(s => s.WorkoutExercise.Workout.UserId == c.World.User && s.WorkoutExercise.Workout.WorkoutDate == today)
                    .Select(s => new { s.WorkoutExercise.Exercise.Name, s.Reps, s.WeightKg }).ToListAsync());
                var bench = sets.Where(s => s.Name == "Bench Press").ToList();
                var row = sets.Where(s => s.Name == "Barbell Row").ToList();
                return bench.Count == 3 && bench.All(s => s.Reps == 5 && s.WeightKg == 62.5m) && row.Count == 3 && row.All(s => s.Reps == 8 && s.WeightKg == 50m)
                    ? null
                    : $"today's sets: {string.Join("; ", sets.Select(s => $"{s.Name} {s.Reps}x{s.WeightKg}"))}";
            }),
        new("train-heaviest-bench", "training", EvalActor.User,
            _ => "What's the heaviest weight I've bench pressed in the last two weeks?",
            ["list_workouts", "get_workout", "get_workout_stats", "list_exercises"],
            ["get_training:history", "get_training:stats", "get_training:exercises"],
            c => Task.FromResult(Mentions(c.Answer, 60, 0, "60 kg"))),
        new("train-template", "training", EvalActor.User,
            _ => "Create a workout template called Leg day: back squat 4 sets of 6, Romanian deadlift 3 sets of 8, leg press 3 sets of 12.",
            ["list_exercises", "create_workout_template", "list_workout_templates"],
            ["get_training:exercises", "save_workout_template:template", "get_training:templates"],
            async c =>
            {
                var templates = await c.Db(db => db.WorkoutTemplates.Where(t => t.UserId == c.World.User && t.Name.ToLower() == "leg day")
                    .Select(t => t.Exercises.OrderBy(e => e.SortOrder).Select(e => new { e.Exercise.Name, e.Sets, e.RepsPerSet }).ToList()).ToListAsync());
                if (templates.Count != 1) return $"{templates.Count} templates called Leg day";
                var got = string.Join("; ", templates[0].Select(e => $"{e.Name} {e.Sets}x{e.RepsPerSet}"));
                return got == "Back Squat 4x6; Romanian Deadlift 3x8; Leg Press 3x12" ? null : got;
            }),
        new("train-run", "training", EvalActor.User,
            _ => "I ran 5 km outside this morning in 28 minutes. Log it.",
            ["list_exercises", "log_workout", "create_exercise"],
            ["get_training:exercises", "log_workout:log", "save_workout_template:exercise"],
            async c =>
            {
                var today = c.World.Today;
                var sets = await c.Db(db => db.ExerciseSets.Where(s => s.WorkoutExercise.Workout.UserId == c.World.User && s.WorkoutExercise.Workout.WorkoutDate == today)
                    .Select(s => new { s.DistanceMeters, s.DurationSeconds }).ToListAsync());
                return sets.Count == 1 && Near(sets[0].DistanceMeters, 5000, 10) && Near(sets[0].DurationSeconds, 1680, 5)
                    ? null
                    : $"today's sets: {string.Join("; ", sets.Select(s => $"{s.DistanceMeters} m in {s.DurationSeconds} s"))}";
            }),

        // Body and progress
        new("body-weight", "body", EvalActor.User,
            _ => "I weighed 80.4 kg this morning.",
            ["log_body_measurement", "list_body_measurements"],
            ["log_body:measurement", "get_body_trend"],
            async c =>
            {
                var today = c.World.Today;
                var weights = await c.Db(db => db.BodyMeasurements.Where(m => m.UserId == c.World.User && m.MeasurementDate == today).Select(m => m.WeightKg).ToListAsync());
                return weights.Count == 1 && weights[0] == 80.4m ? null : $"today's weights: {string.Join(",", weights)}";
            }),
        new("body-trend", "body", EvalActor.User,
            _ => "How much weight have I lost over the last two weeks?",
            ["list_body_measurements"],
            ["get_body_trend"],
            c => Task.FromResult(Mentions(c.Answer, 1.3m, 0.05m, "a 1.3 kg loss"))),
        new("body-goal", "body", EvalActor.User,
            _ => "Set me a new goal: lose weight down to 77 kg, eating 1900 kcal a day with 140 g of protein.",
            ["create_goal", "get_current_goal"],
            ["set_goal:set", "get_nutrition:day", "get_nutrition:goals"],
            async c =>
            {
                var goals = await c.Db(db => db.UserGoals.Where(g => g.UserId == c.World.User && g.IsActive).ToListAsync());
                if (goals.Count != 1) return $"{goals.Count} active goals";
                var g = goals[0];
                return g.GoalType == "weight_loss" && g.TargetWeight == 77 && g.TargetCalories == 1900 && g.TargetProteinGrams == 140
                    ? null
                    : $"goal {g.GoalType} {g.TargetWeight} kg {g.TargetCalories} kcal {g.TargetProteinGrams} g protein";
            }),

        // Social
        new("social-profile", "social", EvalActor.User,
            _ => "Set up my social profile: display name Sara K, bio \"Lifting and lentils\".",
            ["get_social_profile", "save_social_profile"],
            ["get_social:profile", "social_action:profile"],
            async c =>
            {
                var profile = await c.Db(db => db.SocialProfiles.SingleOrDefaultAsync(p => p.UserId == c.World.User));
                return profile is { DisplayName: "Sara K" } && profile.Bio?.Contains("Lifting and lentils", StringComparison.OrdinalIgnoreCase) == true
                    ? null
                    : profile is null ? "no profile" : $"profile {profile.DisplayName} / {profile.Bio}";
            }),
        new("social-accept", "social", EvalActor.User,
            _ => "Dawit asked to follow me. Accept it.",
            ["list_social_follows", "respond_social_follow", "get_social_profile"],
            ["get_social:follows", "social_action:answer_follow"],
            async c =>
            {
                var status = await c.Db(db => db.Follows.Where(f => f.FollowerUserId == c.World.Friend && f.FolloweeUserId == c.World.User).Select(f => f.Status).SingleAsync());
                return status == "Accepted" ? null : $"the follow is {status}";
            }),

        // Coaching, as the trainer
        new("coach-protein", "coaching", EvalActor.Trainer,
            _ => "How much protein did my client Sara Kebede eat yesterday?",
            ["list_trainer_clients", "get_client_nutrition"],
            ["get_clients:clients", "get_clients:nutrition"],
            c => Task.FromResult(Mentions(c.Answer, c.World.ProteinYesterday, 1, "yesterday's protein"))),
        new("coach-accept", "coaching", EvalActor.Trainer,
            _ => "Omar Said sent me a coaching request. Accept him as a client.",
            ["list_trainer_pending_requests", "respond_to_trainer_request", "list_trainer_clients"],
            ["get_clients:requests", "manage_clients:answer_request", "get_clients:clients"],
            async c =>
            {
                var status = await c.Db(db => db.TrainerClientRelationships.Where(r => r.TrainerId == c.World.Trainer && r.ClientId == c.World.Requester).Select(r => r.Status).SingleAsync());
                return status == "active" ? null : $"the relationship is {status}";
            }),
        new("coach-message", "coaching", EvalActor.Trainer,
            _ => "Send my client Sara Kebede this message: \"Great week! Keep protein above 120 g a day.\"",
            ["list_trainer_clients", "send_coach_message", "get_coach_chat"],
            ["get_clients:clients", "manage_clients:message", "get_clients:chat"],
            async c =>
            {
                var messages = await c.Db(db => db.ChatMessages.Where(m => m.SenderId == c.World.Trainer && m.Conversation.Relationship.ClientId == c.World.User)
                    .Select(m => m.Content).ToListAsync());
                return messages.Count == 1 && messages[0].Contains("120") ? null : $"{messages.Count} messages to Sara";
            }),

        // Administration, as the administrator
        new("admin-lookup", "admin", EvalActor.Admin,
            w => $"Which plan is the account {w.UserEmail} on, and is it banned?",
            ["admin_list_users", "admin_get_user"],
            ["admin_lookup:user", "find_action:search", "find_action:describe", "find_action:run"],
            c => Task.FromResult(Regex.IsMatch(c.Answer, @"\bpro\b", RegexOptions.IgnoreCase) ? null : "the answer does not say Pro")),
        new("admin-delete-achievement", "admin", EvalActor.Admin,
            w => $"The achievement \"{w.AchievementName}\" was created by mistake. Delete it.",
            ["admin_get_achievement_analytics", "admin_get_achievement", "admin_delete_achievement", "list_achievements"],
            ["find_action:search", "find_action:describe", "find_action:run", "run_action:admin_delete_achievement", "confirm_action"],
            async c => await c.Db(db => db.Achievements.AnyAsync(a => a.Id == c.World.Achievement)) ? "the achievement is still there" : null,
            FollowUp: "Yes, I confirm. Delete it."),
    ];

    public static string? FollowUpFor(EvalCase evalCase) => evalCase.FollowUp ?? GoAhead;

    public static DateOnly NextMonday(DateOnly today)
    {
        var days = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        return today.AddDays(days == 0 ? 7 : days);
    }

    private static async Task<string?> Calories(EvalContext c, DateOnly date, string meal, string food, decimal expected, decimal tolerance)
    {
        var total = await c.Db(db => db.FoodDiaryEntries.Where(e => e.UserId == c.World.User && e.EntryDate == date && e.MealType == meal && e.Food!.Name == food)
            .SumAsync(e => e.Calories ?? 0));
        return Math.Abs(total - expected) <= expected * tolerance ? null : $"{food} at {meal} is {total:0.#} kcal, expected {expected:0.#}";
    }

    private static async Task<string?> Range(EvalContext c, DateOnly date, string meal, string food, decimal low, decimal high)
    {
        var total = await c.Db(db => db.FoodDiaryEntries.Where(e => e.UserId == c.World.User && e.EntryDate == date && e.MealType == meal && e.Food!.Name == food)
            .SumAsync(e => e.Calories ?? 0));
        return total >= low && total <= high ? null : $"{food} at {meal} is {total:0.#} kcal, expected {low}-{high}";
    }

    private static async Task<string?> RecipeCalories(EvalContext c, DateOnly date, string meal, Guid recipe, decimal expected)
    {
        var rows = await c.Db(db => db.FoodDiaryEntries.Where(e => e.UserId == c.World.User && e.EntryDate == date && e.MealType == meal && e.RecipeId == recipe)
            .Select(e => e.Calories ?? 0).ToListAsync());
        var total = rows.Sum();
        return rows.Count > 0 && Math.Abs(total - expected) <= expected * 0.05m ? null : $"the recipe at {meal} on {date} is {total:0.#} kcal in {rows.Count} rows, expected {expected:0.#}";
    }

    private static async Task<string?> MealTotal(EvalContext c, DateOnly date, string meal, decimal low, decimal high)
    {
        var total = await c.Db(db => db.FoodDiaryEntries.Where(e => e.UserId == c.World.User && e.EntryDate == date && e.MealType == meal).SumAsync(e => e.Calories ?? 0));
        return total >= low && total <= high ? null : $"{meal} on {date} is {total:0.#} kcal, expected {low}-{high}";
    }

    private static string? Once(int count, string what) => count == 1 ? null : $"{count} rows for {what}";

    private static bool Near(decimal? value, decimal expected, decimal tolerance) => value is { } v && Math.Abs(v - expected) <= tolerance;

    private static bool Near(int? value, decimal expected, decimal tolerance) => value is { } v && Math.Abs(v - expected) <= tolerance;

    /// <summary>Whether the answer states a number within <paramref name="tolerance"/> of <paramref name="expected"/>.</summary>
    public static string? Mentions(string answer, decimal expected, decimal tolerance, string what)
    {
        foreach (Match match in Regex.Matches(answer, @"\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?"))
        {
            if (decimal.TryParse(match.Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                && Math.Abs(number - expected) <= tolerance)
            {
                return null;
            }
        }
        return $"the answer does not state {what} ({expected:0.##})";
    }
}
