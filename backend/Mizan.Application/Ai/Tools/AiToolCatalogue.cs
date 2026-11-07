using Mizan.Domain.Goals;
using System.Text.Json;
using Mizan.Application.Commands;
using Mizan.Application.Interfaces;
using Mizan.Application.Queries;
using Mizan.Application.Tools;
using Mizan.Domain.Ai;
using Mizan.Domain.Constants;
using Mizan.Domain.Nutrition;

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
/// 3. Nothing destructive is here. Every tool creates or records; none deletes
///    a record, and none overwrites something the user cannot see afterwards.
///    The one exception is a draft meal plan: its entries may be changed and
///    removed, because the person reviews the draft before accepting it, and
///    the commands refuse the same change on a plan that is no longer a draft.
///
/// Onboarding has its own short list here. The chat assistant uses the task
/// tools (<see cref="TaskToolCatalogue"/>) through <see cref="Assistant"/>, the
/// same definitions MCP uses, so the two cannot drift into different ideas of
/// what an assistant may do (docs/AI.md).
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
            $$"""
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "goalType": { "type": "string", "enum": {{GoalTypes.Json}} },
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
            "Record something the user says they ate. For one of their recipes (search_recipes), pass recipeId and servings "
            + "and leave the macros out: the recipe's own nutrition is used. Otherwise pass a name with the macros they gave you or your best estimate.",
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "recipeId": { "type": "string", "description": "A recipe id from search_recipes" },
                "servings": { "type": "number", "description": "Recipe servings eaten, defaults to 1" },
                "name": { "type": "string" },
                "mealType": { "type": "string", "enum": ["BREAKFAST", "LUNCH", "DINNER", "SNACK", "DRINK"] },
                "calories": { "type": "number" },
                "proteinGrams": { "type": "number" },
                "carbsGrams": { "type": "number" },
                "fatGrams": { "type": "number" },
                "date": { "type": "string", "description": "YYYY-MM-DD, defaults to today" }
              },
              "required": []
            }
            """,
            (args, _) => new CreateFoodDiaryEntryCommand
            {
                RecipeId = AiToolArgs.OptionalGuid(args, "recipeId"),
                Name = AiToolArgs.OptionalString(args, "name"),
                MealType = MealTypes.Normalize(AiToolArgs.OptionalString(args, "mealType")),
                EntryDate = AiToolArgs.OptionalDate(args, "date"),
                Servings = AiToolArgs.OptionalDecimal(args, "servings") ?? 1,
                Calories = AiToolArgs.OptionalDecimal(args, "calories"),
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
            "Create an empty meal plan starting on a date and running for a number of days (default 7). Entries are added later, by the user.",
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "name": { "type": "string" },
                "startDate": { "type": "string", "description": "YYYY-MM-DD" },
                "durationDays": { "type": "integer", "minimum": 1, "maximum": 31 }
              },
              "required": ["startDate"]
            }
            """,
            (args, _) => new Planning.CreateMealPlanCommand
            {
                Name = AiToolArgs.OptionalString(args, "name"),
                StartDate = AiToolArgs.OptionalDate(args, "startDate")
                    ?? throw new Exceptions.DomainValidationException("'startDate' is required."),
                DurationDays = AiToolArgs.OptionalInt(args, "durationDays") ?? 7,
                // Never from the model: a household id it guessed would be an
                // attempt to write into someone else's household.
                HouseholdId = null,
            },
            result => result is Mizan.Contracts.Planning.MealPlanCreatedResult plan && !string.IsNullOrWhiteSpace(plan.Name)
                ? $"Created the plan \"{plan.Name}\"."
                : "Created your meal plan.",
            DataAxis.Nutrition, AiToolAccess.Write),
    ];

    public static AiToolDefinition? FindOnboarding(string name) =>
        Onboarding.FirstOrDefault(tool => tool.Name == name);

    /// <summary>
    /// The assistant proper: the task tools' assistant view (docs/AI.md#tools). Each tool is
    /// offered with its mode argument narrowed to the modes the assistant may use, so a social,
    /// coaching, admin or settings mode is never offered and its only delete is a draft's plan entry.
    /// </summary>
    public static IReadOnlyList<AiToolSpec> Assistant(ToolCallContext context) =>
        TaskToolCatalogue.All
            .Select(tool => ToolFilter.Describe(tool, context))
            .OfType<Contracts.Tools.ToolDescriptorDto>()
            .Select(tool => new AiToolSpec(tool.Name, tool.Description, tool.InputSchema.GetRawText()))
            .ToList();

    /// <summary>
    /// The consent axis a mode is checked on: its own <see cref="ToolMode.Axis"/>, else the one its
    /// area names. Null for an area that has no axis, which the assistant refuses.
    /// </summary>
    public static DataAxis? AxisOf(ToolMode mode)
    {
        if (mode.Axis is { } axis) return axis;
        var area = mode.Area.Split(':')[0];
        return area switch
        {
            "nutrition" => DataAxis.Nutrition,
            "training" => DataAxis.Training,
            "body" => DataAxis.Body,
            "planning" => DataAxis.Planning,
            _ => null,
        };
    }

    /// <summary>The ranking in words: what is left, then each recipe with one serving and what it would leave.</summary>
    internal static string RecipeFitText(Contracts.Nutrition.RecipeFitResult fit)
    {
        static string Amounts(decimal? kcal, decimal? protein, decimal? carbs, decimal? fat) => string.Join(", ", new[]
        {
            kcal is { } k ? $"{k:0} kcal" : null,
            protein is { } p ? $"P {p:0.#} g" : null,
            carbs is { } c ? $"C {c:0.#} g" : null,
            fat is { } f ? $"F {f:0.#} g" : null,
        }.Where(x => x is not null));

        if (fit.Recipes.Count == 0) return fit.Note ?? "Nothing to rank.";
        var lines = new List<string>
        {
            $"{fit.Date:yyyy-MM-dd} left: {Amounts(fit.CaloriesLeft, fit.ProteinLeft, fit.CarbsLeft, fit.FatLeft)}. Ranked best first by Mizan:",
        };
        lines.AddRange(fit.Recipes.Select(r =>
            $"{r.Rank}. {r.RecipeId} — {r.Title}: one serving {r.Calories:0} kcal, P{r.Protein:0.#} C{r.Carbs:0.#} F{r.Fat:0.#}; "
            + $"leaves {Amounts(r.CaloriesLeftAfter, r.ProteinLeftAfter, r.CarbsLeftAfter, r.FatLeftAfter)}"));
        return string.Join("\n", lines);
    }
}
