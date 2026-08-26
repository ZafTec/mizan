using System.Text.Json;
using Mizan.Application.Commands;
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
/// </summary>
public static class AiToolCatalogue
{
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
                : "Saved your targets."),

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
            _ => "Recorded your measurement."),

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
            _ => "Logged that meal."),

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
            _ => "Created your household."),

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
                : "Created your meal plan."),
    ];

    public static AiToolDefinition? Find(string name) =>
        Onboarding.FirstOrDefault(tool => tool.Name == name);
}
