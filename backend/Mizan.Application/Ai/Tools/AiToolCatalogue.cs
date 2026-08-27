using System.Text.Json;
using Mizan.Application.Commands;
using Mizan.Application.Queries;
using Mizan.Domain.Ai;
using Mizan.Domain.Constants;

namespace Mizan.Application.Ai.Tools;

/// <summary>
/// The allowlist. The model can reach these and nothing else - not the
/// database, not a controller, not another command that happens to exist.
///
/// Three rules hold for every entry, and they are why this is a table rather
/// than a set of handlers:
///
/// 1. Each one maps to a MediatR command that already exists, so it runs the
///    same FluentValidation the HTTP path runs. There is no second validation
///    story to keep in step.
/// 2. No schema exposes a user id, a household id the caller does not own, or
///    any other field that says whose record this is. Ownership comes from
///    <see cref="AiToolContext"/>, which comes from the session. A model that
///    writes someone else's id into its arguments changes nothing.
/// 3. Nothing destructive is here, ever. Every tool creates or records; none
///    deletes, and none overwrites something the user cannot see afterwards.
///
/// Shared with <c>Mizan.Mcp.Server</c> in spirit and in shape: MCP proved the
/// tool-to-command pattern and this is the same one, so the two do not drift
/// into different ideas of what an assistant may do (docs/REFOCUS.md §10).
///
/// Every entry declares an axis and whether it reads or writes. Nothing runs
/// until <see cref="Mizan.Domain.Entities.UserAiConsent"/> says that user
/// granted that axis for that kind of access, so adding a tool here does not
/// by itself widen what the assistant may do to anyone.
/// </summary>
public static class AiToolCatalogue
{
    /// <summary>
    /// Setup. A deliberately short list: this runs before the user has a log,
    /// so there is nothing to read and only the handful of things worth
    /// recording on the way in.
    /// </summary>
    public static readonly IReadOnlyList<AiToolDefinition> Onboarding =
    [
        new AiToolDefinition(
            "set_targets",
            "Record the user's nutrition goal and daily targets. Use once the user has told you what they are aiming for.",
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "goalType": { "type": "string", "enum": ["weight_loss", "muscle_gain", "maintenance", "general"] },
                "targetCalories": { "type": "number" },
                "targetProteinGrams": { "type": "number" },
                "targetCarbsGrams": { "type": "number" },
                "targetFatGrams": { "type": "number" },
                "targetWeight": { "type": "number" },
                "weightUnit": { "type": "string", "enum": ["kg", "lb"] },
                "targetDate": { "type": "string", "description": "YYYY-MM-DD" }
              },
              "required": ["goalType"]
            }
            """,
            (args, _) => new CreateUserGoalCommand
            {
                GoalType = AiToolArgs.RequiredString(args, "goalType"),
                TargetCalories = AiToolArgs.OptionalInt(args, "targetCalories"),
                TargetProteinGrams = AiToolArgs.OptionalDecimal(args, "targetProteinGrams"),
                TargetCarbsGrams = AiToolArgs.OptionalDecimal(args, "targetCarbsGrams"),
                TargetFatGrams = AiToolArgs.OptionalDecimal(args, "targetFatGrams"),
                TargetWeight = AiToolArgs.OptionalDecimal(args, "targetWeight"),
                WeightUnit = AiToolArgs.OptionalString(args, "weightUnit"),
                TargetDate = AiToolArgs.OptionalDate(args, "targetDate"),
            },
            result => result is CreateUserGoalResult goal && !string.IsNullOrWhiteSpace(goal.Message)
                ? goal.Message
                : "Saved your targets.",
            DataAxis.Nutrition, AiToolAccess.Write),

        new AiToolDefinition(
            "log_measurement",
            "Record a body measurement. Weight is the usual one; the rest are optional.",
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "weightKg": { "type": "number" },
                "bodyFatPercentage": { "type": "number" },
                "muscleMassKg": { "type": "number" },
                "waistCm": { "type": "number" },
                "date": { "type": "string", "description": "YYYY-MM-DD, defaults to today" }
              },
              "required": []
            }
            """,
            (args, ctx) => new LogBodyMeasurementCommand(
                // Not from the arguments. This is the rule the whole catalogue
                // exists to enforce.
                ctx.UserId,
                (AiToolArgs.OptionalDate(args, "date") ?? DateOnly.FromDateTime(DateTime.UtcNow))
                    .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                AiToolArgs.OptionalDecimal(args, "weightKg"),
                AiToolArgs.OptionalDecimal(args, "bodyFatPercentage"),
                AiToolArgs.OptionalDecimal(args, "muscleMassKg"),
                AiToolArgs.OptionalDecimal(args, "waistCm"),
                null, null, null, null, null, null, null),
            _ => "Recorded your measurement.",
            DataAxis.Body, AiToolAccess.Write),

        new AiToolDefinition(
            "log_meal",
            "Record something the user says they ate, with the macros they gave you or your best estimate.",
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "name": { "type": "string" },
                "mealType": { "type": "string", "enum": ["BREAKFAST", "LUNCH", "DINNER", "SNACK", "DRINK"] },
                "calories": { "type": "number" },
                "proteinGrams": { "type": "number" },
                "carbsGrams": { "type": "number" },
                "fatGrams": { "type": "number" },
                "date": { "type": "string", "description": "YYYY-MM-DD, defaults to today" }
              },
              "required": ["name", "calories"]
            }
            """,
            (args, _) => new CreateFoodDiaryEntryCommand
            {
                Name = AiToolArgs.RequiredString(args, "name"),
                MealType = MealTypes.Normalize(AiToolArgs.OptionalString(args, "mealType")),
                EntryDate = AiToolArgs.OptionalDate(args, "date"),
                Servings = 1,
                Calories = AiToolArgs.RequiredDecimal(args, "calories"),
                ProteinGrams = AiToolArgs.OptionalDecimal(args, "proteinGrams"),
                CarbsGrams = AiToolArgs.OptionalDecimal(args, "carbsGrams"),
                FatGrams = AiToolArgs.OptionalDecimal(args, "fatGrams"),
            },
            _ => "Logged that meal.",
            DataAxis.Nutrition, AiToolAccess.Write),

        new AiToolDefinition(
            "create_household",
            "Create a household so the user can share plans and shopping with the people they cook for.",
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": { "name": { "type": "string" } },
              "required": ["name"]
            }
            """,
            (args, ctx) => new CreateHouseholdCommand(AiToolArgs.RequiredString(args, "name"), ctx.UserId),
            _ => "Created your household.",
            DataAxis.Nutrition, AiToolAccess.Write),

        new AiToolDefinition(
            "create_meal_plan",
            "Create an empty meal plan over a date range. Recipes are added later, by the user.",
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "name": { "type": "string" },
                "startDate": { "type": "string", "description": "YYYY-MM-DD" },
                "endDate": { "type": "string", "description": "YYYY-MM-DD" }
              },
              "required": ["startDate", "endDate"]
            }
            """,
            (args, _) => new CreateMealPlanCommand
            {
                Name = AiToolArgs.OptionalString(args, "name"),
                StartDate = AiToolArgs.OptionalDate(args, "startDate")
                    ?? throw new Exceptions.DomainValidationException("'startDate' is required."),
                EndDate = AiToolArgs.OptionalDate(args, "endDate")
                    ?? throw new Exceptions.DomainValidationException("'endDate' is required."),
                // Never from the model: a household id it guessed would be an
                // attempt to write into someone else's household.
                HouseholdId = null,
            },
            result => result is CreateMealPlanResult plan && !string.IsNullOrWhiteSpace(plan.Name)
                ? $"Created the plan \"{plan.Name}\"."
                : "Created your meal plan.",
            DataAxis.Nutrition, AiToolAccess.Write),
    ];

    /// <summary>
    /// The assistant proper: everything onboarding has, plus the things that
    /// only make sense once there is a log to act on, plus the reads it needs
    /// to act sensibly - you cannot record a workout against an exercise
    /// without first finding its id.
    /// </summary>
    public static readonly IReadOnlyList<AiToolDefinition> Chat =
    [
        .. Onboarding,

        new AiToolDefinition(
            "log_workout",
            "Record a training session the user describes. Call search_exercises first to get the id for each movement; never guess one.",
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "name": { "type": "string" },
                "date": { "type": "string", "description": "YYYY-MM-DD, defaults to today" },
                "durationMinutes": { "type": "integer" },
                "notes": { "type": "string" },
                "exercises": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "additionalProperties": false,
                    "properties": {
                      "exerciseId": { "type": "string", "description": "From search_exercises" },
                      "notes": { "type": "string" },
                      "sets": {
                        "type": "array",
                        "items": {
                          "type": "object",
                          "additionalProperties": false,
                          "properties": {
                            "reps": { "type": "integer" },
                            "weightKg": { "type": "number" },
                            "durationSeconds": { "type": "integer" },
                            "distanceMeters": { "type": "number" }
                          }
                        }
                      }
                    },
                    "required": ["exerciseId", "sets"]
                  }
                }
              },
              "required": ["exercises"]
            }
            """,
            (args, _) => new LogWorkoutCommand
            {
                Name = AiToolArgs.OptionalString(args, "name"),
                WorkoutDate = AiToolArgs.OptionalDate(args, "date")
                    ?? DateOnly.FromDateTime(DateTime.UtcNow),
                DurationMinutes = AiToolArgs.OptionalInt(args, "durationMinutes"),
                Notes = AiToolArgs.OptionalString(args, "notes"),
                Exercises = AiToolArgs.Workouts.Exercises(args),
            },
            result => result is LogWorkoutResult workout
                ? $"Logged {workout.TotalExercises} exercise(s), {workout.TotalSets} set(s)."
                : "Logged that workout.",
            DataAxis.Training, AiToolAccess.Write),

        new AiToolDefinition(
            "search_exercises",
            "Find exercises by name to get the ids log_workout needs. Returns at most ten matches.",
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "query": { "type": "string" },
                "muscleGroup": { "type": "string" }
              },
              "required": ["query"]
            }
            """,
            (args, _) => new GetExercisesQuery
            {
                SearchTerm = AiToolArgs.RequiredString(args, "query"),
                MuscleGroup = AiToolArgs.OptionalString(args, "muscleGroup"),
                PageSize = 10,
            },
            result => result is GetExercisesResult exercises && exercises.Items.Count > 0
                ? string.Join("\n", exercises.Items.Select(e => $"{e.Id} — {e.Name}"))
                : "No exercises matched that.",
            DataAxis.Training, AiToolAccess.Read),

        new AiToolDefinition(
            "search_foods",
            "Look up foods already in the database, with their macros per serving.",
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": { "query": { "type": "string" } },
              "required": ["query"]
            }
            """,
            (args, _) => new SearchFoodsQuery
            {
                SearchTerm = AiToolArgs.RequiredString(args, "query"),
                PageSize = 10,
            },
            result => result is Common.PagedResult<FoodDto> foods && foods.Items.Count > 0
                ? string.Join("\n", foods.Items.Select(f =>
                    $"{f.Name} (per 100g) - {f.CaloriesPer100g} kcal, "
                    + $"P{f.ProteinPer100g} C{f.CarbsPer100g} F{f.FatPer100g}"))
                : "No foods matched that.",
            DataAxis.Nutrition, AiToolAccess.Read),

        new AiToolDefinition(
            "get_daily_totals",
            "The user's totals against their targets for one day. Use before answering anything about how their day is going.",
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": { "date": { "type": "string", "description": "YYYY-MM-DD, defaults to today" } },
              "required": []
            }
            """,
            (args, _) => new GetDailyNutritionQuery
            {
                Date = AiToolArgs.OptionalDate(args, "date") ?? DateOnly.FromDateTime(DateTime.UtcNow),
            },
            result => result is DailyNutritionResult day
                ? $"{day.Date:yyyy-MM-dd}: {day.TotalCalories} kcal "
                  + $"(target {day.TargetCalories?.ToString() ?? "none"}), "
                  + $"P{day.TotalProtein} C{day.TotalCarbs} F{day.TotalFat}"
                : "No totals for that day.",
            DataAxis.Nutrition, AiToolAccess.Read),

        new AiToolDefinition(
            "record_goal_progress",
            "Record a day's actuals against the user's goal, for the progress chart.",
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "actualCalories": { "type": "integer" },
                "actualProteinGrams": { "type": "number" },
                "actualCarbsGrams": { "type": "number" },
                "actualFatGrams": { "type": "number" },
                "actualWeight": { "type": "number" },
                "date": { "type": "string", "description": "YYYY-MM-DD, defaults to today" },
                "notes": { "type": "string" }
              },
              "required": ["actualCalories"]
            }
            """,
            (args, _) => new RecordGoalProgressCommand
            {
                ActualCalories = AiToolArgs.OptionalInt(args, "actualCalories") ?? 0,
                ActualProteinGrams = AiToolArgs.OptionalDecimal(args, "actualProteinGrams") ?? 0,
                ActualCarbsGrams = AiToolArgs.OptionalDecimal(args, "actualCarbsGrams") ?? 0,
                ActualFatGrams = AiToolArgs.OptionalDecimal(args, "actualFatGrams") ?? 0,
                ActualWeight = AiToolArgs.OptionalDecimal(args, "actualWeight"),
                Date = AiToolArgs.OptionalDate(args, "date"),
                Notes = AiToolArgs.OptionalString(args, "notes"),
            },
            _ => "Recorded your progress for that day.",
            DataAxis.Nutrition, AiToolAccess.Write),

        new AiToolDefinition(
            "create_shopping_list",
            "Start a shopping list. Items are added with add_shopping_list_item.",
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": { "name": { "type": "string" } },
              "required": ["name"]
            }
            """,
            (args, ctx) => new CreateShoppingListCommand(
                AiToolArgs.RequiredString(args, "name"), ctx.UserId, null),
            _ => "Created that shopping list.",
            DataAxis.Nutrition, AiToolAccess.Write),

        new AiToolDefinition(
            "add_shopping_list_item",
            "Add one item to an existing shopping list.",
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "shoppingListId": { "type": "string" },
                "itemName": { "type": "string" },
                "amount": { "type": "number" },
                "unit": { "type": "string" },
                "category": { "type": "string" }
              },
              "required": ["shoppingListId", "itemName"]
            }
            """,
            (args, _) => new AddShoppingListItemCommand(
                AiToolArgs.RequiredGuid(args, "shoppingListId"),
                AiToolArgs.RequiredString(args, "itemName"),
                AiToolArgs.OptionalDecimal(args, "amount"),
                AiToolArgs.OptionalString(args, "unit"),
                AiToolArgs.OptionalString(args, "category")),
            _ => "Added that to the list.",
            DataAxis.Nutrition, AiToolAccess.Write),
    ];

    public static AiToolDefinition? Find(string name) =>
        Chat.FirstOrDefault(tool => tool.Name == name);
}
