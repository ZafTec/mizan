using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Mizan.Contracts.Meals;
using Mizan.Contracts.Measurements;
using Mizan.Contracts.Workouts;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class MealTools
{
    private readonly IBackendApiClient _api;

    public MealTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "log_day", Idempotent = false)]
    [Description("Log meals, workout sets, and a weight for one date. mealsJson is an array of {foodId OR recipeId OR name,mealType,servings?,calories?,proteinGrams?,carbsGrams?,fatGrams?}. exercisesJson is an array of {exerciseId,sets:[{reps?,weightKg?,durationSeconds?,distanceMeters?}]}. Uses the existing logging APIs. If a write fails, returns a receipt of completed, failed, and remaining items: retry only remaining items, and check any unconfirmed write in the diary before retrying it.")]
    public async Task<CallToolResult> LogDay(
        [Description("Date to log, YYYY-MM-DD")] string date,
        [Description("Optional JSON array of meal entries")] string? mealsJson = null,
        [Description("Optional JSON array of exercises with their sets for one workout")] string? exercisesJson = null,
        [Description("Optional body weight in kilograms")] decimal? weightKg = null,
        [Description("Workout name, when exercises are supplied")] string? workoutName = null,
        [Description("Workout duration in minutes, when exercises are supplied")] int? workoutDurationMinutes = null,
        CancellationToken ct = default)
    {
        var day = ToolArguments.ParseDate(date, "date");
        var meals = ParseList<LogMealRequest>(mealsJson, "mealsJson");
        var exercises = ParseList<WorkoutExerciseDto>(exercisesJson, "exercisesJson");
        if (meals.Count == 0 && exercises.Count == 0 && !weightKg.HasValue)
            throw new ArgumentException("Supply mealsJson, exercisesJson, or weightKg to log a day.");
        if (meals.Count > 100) throw new ArgumentException("A day can contain at most 100 meal entries.", nameof(mealsJson));
        if (weightKg is <= 0 or > 1000) throw new ArgumentException("weightKg must be greater than zero and at most 1000.", nameof(weightKg));
        if (workoutName?.Length > 100) throw new ArgumentException("workoutName must be at most 100 characters.", nameof(workoutName));
        if (workoutDurationMinutes is < 1 or > 1440) throw new ArgumentException("workoutDurationMinutes must be between 1 and 1440.", nameof(workoutDurationMinutes));
        if (exercises.Count > 0 && weightKg is < 20 or > 500) throw new ArgumentException("A workout bodyweight must be between 20 and 500 kg.", nameof(weightKg));
        ValidateExercises(exercises);

        var writes = new List<DayWrite>();
        for (var index = 0; index < meals.Count; index++)
        {
            var meal = meals[index];
            if (meal is null || meal.Servings <= 0 || (meal.FoodId.HasValue && meal.RecipeId.HasValue)
                || meal.FoodId == Guid.Empty || meal.RecipeId == Guid.Empty
                || (!meal.FoodId.HasValue && !meal.RecipeId.HasValue && string.IsNullOrWhiteSpace(meal.Name)))
                throw new ArgumentException($"mealsJson[{index}] needs one foodId, recipeId, or name, and positive servings.", nameof(mealsJson));
            if (meal.EntryDate.HasValue && meal.EntryDate != day)
                throw new ArgumentException($"mealsJson[{index}].entryDate must match date.", nameof(mealsJson));
            if (new[] { meal.Calories, meal.ProteinGrams, meal.CarbsGrams, meal.FatGrams, meal.FiberGrams }.Any(value => value < 0))
                throw new ArgumentException($"mealsJson[{index}] nutrition values cannot be negative.", nameof(mealsJson));
            writes.Add(new DayWrite("meal", index, "/api/Meals", meal with { EntryDate = day, MealType = NormalizeMealType(meal.MealType) }));
        }
        if (exercises.Count > 0)
        {
            // The workout endpoint persists its BodyweightKg as a measurement
            // in the same transaction; a second measurement POST would duplicate it.
            writes.Add(new DayWrite("workout", null, "/api/Workouts", new LogWorkoutRequest
            {
                WorkoutDate = day, Name = workoutName, DurationMinutes = workoutDurationMinutes,
                Exercises = exercises, BodyweightKg = weightKg,
            }));
        }
        else if (weightKg.HasValue)
        {
            writes.Add(new DayWrite("measurement", null, "/api/BodyMeasurements", new LogMeasurementRequest(
                Date: day.ToDateTime(TimeOnly.MinValue), WeightKg: weightKg, BodyFatPercentage: null,
                MuscleMassKg: null, WaistCm: null, HipsCm: null, ChestCm: null, LeftArmCm: null,
                RightArmCm: null, LeftThighCm: null, RightThighCm: null, Notes: null)));
        }

        var completed = new List<object>();
        for (var index = 0; index < writes.Count; index++)
        {
            var write = writes[index];
            try
            {
                var response = await _api.PostAsync(write.Endpoint, write.Body, ct);
                completed.Add(new { write.Kind, write.MealIndex, response = JsonSerializer.Deserialize<JsonElement>(response),
                    includesWeight = write.Kind == "workout" && weightKg.HasValue });
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // A server/network failure can happen after commit. Do not tell
                // the caller it is safe to replay an unconfirmed write.
                var unconfirmed = error is not BackendApiException backendError || (int)backendError.Status >= 500;
                return DayResult(new
                {
                    success = false, date = day, completed,
                    failed = new { write.Kind, write.MealIndex, error = error.Message, mayHaveBeenSaved = unconfirmed },
                    remaining = writes.Skip(index + (unconfirmed ? 1 : 0)).Select(item => new { item.Kind, item.MealIndex }),
                    guidance = unconfirmed
                        ? "Check the failed item in the diary before retrying it; its save is unconfirmed. Do not replay completed items."
                        : "Completed items are saved. Retry only the remaining items; do not replay the whole day."
                }, true);
            }
        }
        return DayResult(new { success = true, date = day, completed }, false);
    }

    private static readonly JsonSerializerOptions DayJson = new(JsonSerializerDefaults.Web);
    private sealed record DayWrite(string Kind, int? MealIndex, string Endpoint, object Body);
    private static CallToolResult DayResult(object result, bool error) => new()
    {
        IsError = error,
        Content = [new TextContentBlock { Text = JsonSerializer.Serialize(result, DayJson) }]
    };

    private static List<T> ParseList<T>(string? json, string name)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<T>>(json, DayJson) ?? throw new ArgumentException($"{name} must be a JSON array.", name); }
        catch (JsonException error) { throw new ArgumentException($"Invalid {name}: {error.Message}", name); }
    }

    private static void ValidateExercises(List<WorkoutExerciseDto> exercises)
    {
        if (exercises.Count > 30) throw new ArgumentException("exercisesJson can contain at most 30 exercises.");
        for (var index = 0; index < exercises.Count; index++)
        {
            var exercise = exercises[index];
            if (exercise is null || exercise.ExerciseId == Guid.Empty || exercise.Notes?.Length > 500
                || exercise.Sets is null || exercise.Sets.Count is < 1 or > 50)
                throw new ArgumentException($"exercisesJson[{index}] needs an exerciseId and 1 to 50 sets; notes may contain at most 500 characters.");
            if (exercise.Sets.Any(set => set is null || set.Reps is < 0 or > 1000 || set.WeightKg is < 0 or > 1000
                || set.DurationSeconds is < 0 or > 86400 || set.DistanceMeters is < 0 or > 1000000 || set.Steps is < 0 or > 200000))
                throw new ArgumentException($"exercisesJson[{index}] contains an invalid set. Check reps, weight, duration, distance, and steps.");
        }
    }

    [McpServerTool(Name = "get_food_diary", ReadOnly = true, Idempotent = true)]
    [Description("Get the food diary for a specific date. Shows all meals logged with macros.")]
    public async Task<string> GetFoodDiary(
        [Description("Date in YYYY-MM-DD format (defaults to today)")] string? date = null,
        CancellationToken ct = default)
    {
        var qs = "/api/Meals";
        if (!string.IsNullOrEmpty(date)) qs += $"?date={date}";
        return await _api.GetAsync(qs, ct);
    }

    [McpServerTool(Name = "get_daily_nutrition", ReadOnly = true, Idempotent = true)]
    [Description("Get daily nutrition totals for a specific date. Shows calories, protein, carbs, fat breakdown.")]
    public async Task<string> GetDailyNutrition(
        [Description("Date in YYYY-MM-DD format (defaults to today)")] string? date = null,
        CancellationToken ct = default)
    {
        var qs = "/api/Meals/range?days=1";
        if (!string.IsNullOrEmpty(date)) qs += $"&endDate={date}";
        var raw = await _api.GetAsync(qs, ct);

        using var doc = JsonDocument.Parse(raw);
        var days = doc.RootElement.GetProperty("days");

        if (days.GetArrayLength() == 0)
        {
            var targetDate = date ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
            return JsonSerializer.Serialize(new { date = targetDate, totalCalories = 0m, totalProtein = 0m, totalCarbs = 0m, totalFat = 0m });
        }

        var day = days[0];
        return JsonSerializer.Serialize(new
        {
            date = day.GetProperty("date").ToString(),
            totalCalories = day.GetProperty("calories").GetDecimal(),
            totalProtein = day.GetProperty("protein").GetDecimal(),
            totalCarbs = day.GetProperty("carbs").GetDecimal(),
            totalFat = day.GetProperty("fat").GetDecimal()
        });
    }

    [McpServerTool(Name = "get_nutrition_range", ReadOnly = true, Idempotent = true)]
    [Description("Get daily nutrition summary over a date range. Useful for trends and weekly/monthly overview.")]
    public async Task<string> GetNutritionRange(
        [Description("Number of days to look back (1-90, default 7)")] int days = 7,
        [Description("End date in YYYY-MM-DD format (defaults to today)")] string? endDate = null,
        CancellationToken ct = default)
    {
        var qs = $"/api/Meals/range?days={Math.Clamp(days, 1, 90)}";
        if (!string.IsNullOrEmpty(endDate)) qs += $"&endDate={endDate}";
        return await _api.GetAsync(qs, ct);
    }

    [McpServerTool(Name = "log_food")]
    [Description("Log a food item to the food diary. Use search_foods first to get a foodId.")]
    public async Task<string> LogFood(
        [Description("Food UUID from search_foods")] string foodId,
        [Description("Date in YYYY-MM-DD format")] string date,
        [Description("Meal category: BREAKFAST, LUNCH, DINNER, SNACK, or DRINK")] string mealType = "BREAKFAST",
        [Description("Number of servings (default 1)")] decimal servings = 1,
        [Description("Optional ISO 8601 timestamp (e.g. 2026-04-20T16:14:54Z) of when the meal was eaten; defaults to now")] string? loggedAt = null,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/Meals", new LogMealRequest
        {
            EntryDate = ToolArguments.ParseOptionalDate(date, "date"),
            MealType = NormalizeMealType(mealType),
            Servings = servings,
            FoodId = ToolArguments.ParseId(foodId, "foodId"),
            LoggedAt = ToolArguments.ParseOptionalTimestamp(loggedAt, "loggedAt"),
        }, ct);
    }

    [McpServerTool(Name = "log_meal")]
    [Description("Log a recipe to the food diary. Use search_recipes or get_recipe first to get a recipeId.")]
    public async Task<string> LogMeal(
        [Description("Recipe UUID from search_recipes")] string recipeId,
        [Description("Date in YYYY-MM-DD format")] string date,
        [Description("Meal category: BREAKFAST, LUNCH, DINNER, SNACK, or DRINK")] string mealType = "BREAKFAST",
        [Description("Number of servings (default 1)")] decimal servings = 1,
        [Description("Optional ISO 8601 timestamp of when the meal was eaten; defaults to now")] string? loggedAt = null,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/Meals", new LogMealRequest
        {
            EntryDate = ToolArguments.ParseOptionalDate(date, "date"),
            MealType = NormalizeMealType(mealType),
            Servings = servings,
            RecipeId = ToolArguments.ParseId(recipeId, "recipeId"),
            LoggedAt = ToolArguments.ParseOptionalTimestamp(loggedAt, "loggedAt"),
        }, ct);
    }

    [McpServerTool(Name = "log_meal_manual")]
    [Description("Log a meal with manual nutrition values when no food/recipe exists in the database.")]
    public async Task<string> LogMealManual(
        [Description("Meal name (e.g. 'Homemade smoothie')")] string name,
        [Description("Date in YYYY-MM-DD format")] string date,
        [Description("Total calories")] decimal calories,
        [Description("Meal category: BREAKFAST, LUNCH, DINNER, SNACK, or DRINK")] string mealType = "BREAKFAST",
        [Description("Number of servings (default 1)")] decimal servings = 1,
        [Description("Protein in grams")] decimal? proteinGrams = null,
        [Description("Carbs in grams")] decimal? carbsGrams = null,
        [Description("Fat in grams")] decimal? fatGrams = null,
        [Description("Fiber in grams")] decimal? fiberGrams = null,
        [Description("Optional ISO 8601 timestamp of when the meal was eaten; defaults to now")] string? loggedAt = null,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/Meals", new LogMealRequest
        {
            EntryDate = ToolArguments.ParseOptionalDate(date, "date"),
            MealType = NormalizeMealType(mealType),
            Servings = servings,
            Name = name,
            Calories = calories,
            ProteinGrams = proteinGrams,
            CarbsGrams = carbsGrams,
            FatGrams = fatGrams,
            FiberGrams = fiberGrams,
            LoggedAt = ToolArguments.ParseOptionalTimestamp(loggedAt, "loggedAt"),
        }, ct);
    }

    [McpServerTool(Name = "delete_meal", Destructive = true)]
    [Description("Delete a food diary entry. This removes the logged meal.")]
    public async Task<string> DeleteMeal(
        [Description("Diary entry UUID")] string id,
        CancellationToken ct = default)
    {
        return await _api.DeleteAsync($"/api/Meals/{id}", ct);
    }

    private static string NormalizeMealType(string mealType)
    {
        return mealType.Trim().ToUpperInvariant() switch
        {
            "BREAKFAST" => "BREAKFAST",
            "LUNCH" => "LUNCH",
            "DINNER" => "DINNER",
            "SNACK" => "SNACK",
            "DRINK" or "BEVERAGE" => "DRINK",
            "MEAL" => "MEAL",
            var raw => throw new ArgumentException($"Meal type '{raw}' is invalid. Use BREAKFAST, LUNCH, DINNER, SNACK, or DRINK.")
        };
    }


}
