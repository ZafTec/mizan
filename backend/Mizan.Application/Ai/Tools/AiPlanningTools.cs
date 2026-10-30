using System.Globalization;
using System.Text;
using Mizan.Application.Planning;
using Mizan.Contracts.Ai;
using Mizan.Contracts.Planning;
using Mizan.Domain.Ai;
using Mizan.Domain.Pricing;

namespace Mizan.Application.Ai.Tools;

/// <summary>
/// The assistant's planning tools (docs/PLANNING.md#ai-planning). Each runs an existing planning
/// command or query on <see cref="DataAxis.Planning"/>: reading follows the nutrition share
/// consent and writing the nutrition write consent, and a connected app needs
/// <c>planning:read</c> or <c>planning:write</c>. What the model reads back is plain text with
/// the ids it needs for the next call; what the person sees is the typed cards built here from
/// the same results.
/// </summary>
public static class AiPlanningTools
{
    private const string EntryProperties = """
                "dayIndex": { "type": "integer", "description": "0 is the plan's first day" },
                "slot": { "type": "string", "enum": ["BREAKFAST", "LUNCH", "DINNER", "SNACK"] },
                "recipeId": { "type": "string", "description": "One of the user's recipes; makes a recipe entry" },
                "servings": { "type": "number" },
                "foodId": { "type": "string", "description": "From search_foods; makes a food entry" },
                "grams": { "type": "number" },
                "text": { "type": "string", "description": "A plain line when there is no recipe or food" },
                "notes": { "type": "string" }
        """;

    public static readonly IReadOnlyList<AiToolDefinition> All =
    [
        new AiToolDefinition(
            "generate_meal_plan",
            "Draft a meal plan from the user's rules, recipes, catalogue foods and prices. One draft per budget in "
            + "budgetCeilings. Nutrition and cost are computed by the app. The draft is not active until the user accepts it in the app.",
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "startDate": { "type": "string", "description": "YYYY-MM-DD" },
                "durationDays": { "type": "integer", "minimum": 1, "maximum": 14 },
                "preference": { "type": "string", "description": "What the user asked for, in their words" },
                "name": { "type": "string" },
                "macroMode": { "type": "string", "enum": ["None", "Keto", "HighProtein", "Custom"] },
                "targetCalories": { "type": "number" },
                "targetProtein": { "type": "number" },
                "maxCarbs": { "type": "number" },
                "targetFat": { "type": "number" },
                "weeklyBudget": { "type": "number" },
                "currency": { "type": "string", "description": "Three letters, e.g. ETB" },
                "prepStyle": { "type": "string", "enum": ["Daily", "Batch"] },
                "budgetCeilings": { "type": "array", "items": { "type": "number" }, "maxItems": 3 },
                "dailyBudget": { "type": "number", "description": "A ceiling for each day's cost, in the budgets' currency" }
              },
              "required": ["startDate"]
            }
            """,
            (args, _) => new GenerateMealPlanCommand
            {
                StartDate = AiToolArgs.OptionalDate(args, "startDate")
                    ?? throw new Exceptions.DomainValidationException("'startDate' is required."),
                DurationDays = AiToolArgs.OptionalInt(args, "durationDays") ?? 7,
                Preference = AiToolArgs.OptionalString(args, "preference"),
                Name = AiToolArgs.OptionalString(args, "name"),
                Rules = AiToolArgs.Planning.Rules(args),
                BudgetCeilings = AiToolArgs.DecimalList(args, "budgetCeilings"),
                DailyBudget = AiToolArgs.OptionalDecimal(args, "dailyBudget"),
            },
            result => result is GenerateMealPlanResult generated ? DescribeGenerated(generated) : "Drafted a plan.",
            DataAxis.Planning, AiToolAccess.Write,
            result => result is GenerateMealPlanResult generated
                ? generated.Drafts.SelectMany(draft => new[]
                {
                    new AiCardDraft(AiCardTypes.PlanPreview, Preview(draft)),
                    new AiCardDraft(AiCardTypes.CostTable, new CostTableCardData(draft.Plan.Id, draft.Cost)),
                }).ToList()
                : []),

        new AiToolDefinition(
            "get_plan_day",
            "One day of a plan with its entries and their ids, totals and violations. Without planId, the user's active plan "
            + "on that date. With planId, that plan (a draft too); the date defaults to its first day.",
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "date": { "type": "string", "description": "YYYY-MM-DD" },
                "planId": { "type": "string" }
              },
              "required": []
            }
            """,
            (args, _) => new GetPlanDayQuery(AiToolArgs.OptionalDate(args, "date"), AiToolArgs.OptionalGuid(args, "planId")),
            result => result is PlanDayViewDto day ? DescribeDay(day) : "There is no plan for that day.",
            DataAxis.Planning, AiToolAccess.Read,
            result => result is PlanDayViewDto day
                ? [new AiCardDraft(AiCardTypes.DayPlan, new DayPlanCardData(day.PlanId, day.PlanName, day.Day))]
                : []),

        new AiToolDefinition(
            "add_plan_entry",
            "Add one entry to a plan: a recipe with servings, a food from search_foods with grams, or a plain text line.",
            $$"""
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "planId": { "type": "string" },
            {{EntryProperties}}
              },
              "required": ["planId", "dayIndex", "slot"]
            }
            """,
            (args, _) => new AddPlanEntryCommand(AiToolArgs.RequiredGuid(args, "planId"), AiToolArgs.Planning.Entry(args)),
            result => result is PlanEntryCreatedResult created ? $"Added entry {created.Id}." : "Added that entry.",
            DataAxis.Planning, AiToolAccess.Write),

        new AiToolDefinition(
            "update_plan_entry",
            "Rewrite one entry of a draft plan, by its entry id from get_plan_day. A plan the user follows is not changed this way.",
            $$"""
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "planId": { "type": "string" },
                "entryId": { "type": "string" },
            {{EntryProperties}}
              },
              "required": ["planId", "entryId", "dayIndex", "slot"]
            }
            """,
            (args, _) => new UpdatePlanEntryCommand(
                AiToolArgs.RequiredGuid(args, "planId"),
                AiToolArgs.RequiredGuid(args, "entryId"),
                AiToolArgs.Planning.Entry(args),
                DraftOnly: true),
            _ => "Updated that entry.",
            DataAxis.Planning, AiToolAccess.Write),

        new AiToolDefinition(
            "remove_plan_entry",
            "Remove one entry from a draft plan, by its entry id from get_plan_day. A plan the user follows is not changed this way.",
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "planId": { "type": "string" },
                "entryId": { "type": "string" }
              },
              "required": ["planId", "entryId"]
            }
            """,
            (args, _) => new RemovePlanEntryCommand(
                AiToolArgs.RequiredGuid(args, "planId"),
                AiToolArgs.RequiredGuid(args, "entryId"),
                DraftOnly: true),
            _ => "Removed that entry.",
            DataAxis.Planning, AiToolAccess.Write),

        new AiToolDefinition(
            "generate_shopping_list",
            "Make a shopping list from a plan. The app adds up the amounts; lines it cannot weigh are marked for review.",
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "planId": { "type": "string" },
                "name": { "type": "string" }
              },
              "required": ["planId"]
            }
            """,
            (args, _) => new GenerateShoppingListCommand(
                AiToolArgs.RequiredGuid(args, "planId"), AiToolArgs.OptionalString(args, "name"), null),
            result => result is GenerateShoppingListResult list
                ? $"Made shopping list {list.ListId} with {list.ItemCount} item(s), {list.NeedsReviewCount} to review."
                : "Made the shopping list.",
            DataAxis.Planning, AiToolAccess.Write,
            result => result is GenerateShoppingListResult list
                ? [new AiCardDraft(AiCardTypes.ShoppingList, new ShoppingListCardData(list.ListId, list.ItemCount, list.NeedsReviewCount))]
                : []),
    ];

    public static PlanPreviewCardData Preview(GeneratedPlanDto draft) => new(
        draft.Plan.Id,
        draft.Plan.Name,
        draft.Plan.Status,
        draft.Plan.StartDate,
        draft.Plan.DurationDays,
        draft.Plan.EntryCount,
        draft.Unresolved.Count,
        draft.BudgetCeiling,
        draft.Cost.WeeklyTotal,
        draft.Cost.Currency,
        draft.Violations);

    /// <summary>What the model reads back: each draft with its ids, entries, cost and problems, in plain lines.</summary>
    private static string DescribeGenerated(GenerateMealPlanResult result)
    {
        var text = new StringBuilder();
        foreach (var draft in result.Drafts)
        {
            var plan = draft.Plan;
            text.Append(CultureInfo.InvariantCulture,
                $"Drafted \"{plan.Name}\" (plan {plan.Id}, {plan.Status}): {plan.DurationDays} days, {plan.EntryCount} entries, "
                + $"{Money.Format(draft.Cost.WeeklyTotal, draft.Cost.Currency)} a week ({draft.Cost.PricedItems} of {draft.Cost.TotalItems} lines priced)");
            if (draft.BudgetCeiling is { } ceiling) text.Append(CultureInfo.InvariantCulture, $" against a {Money.Format(ceiling, plan.Rules.Currency ?? draft.Cost.Currency)} budget");
            text.Append(".\n");
            foreach (var day in plan.Days)
            {
                var entries = day.Slots.SelectMany(s => s.Entries).ToList();
                if (entries.Count == 0) continue;
                text.Append(CultureInfo.InvariantCulture, $"Day {day.DayIndex}: ")
                    .Append(string.Join("; ", entries.Select(Entry)))
                    .Append('\n');
            }
            // Code's verdict, not the model's: what the draft does not meet, with amounts and targets.
            text.Append("Checked: ").Append(draft.Note).Append('\n');
            foreach (var unmet in draft.Unmet)
                text.Append("Not met (days counted from 1): ").Append(unmet).Append('\n');
            foreach (var line in draft.Unresolved)
                text.Append(CultureInfo.InvariantCulture, $"To review, day {line.DayIndex} {line.Slot}: {line.Text} ({line.Reason})\n");
        }
        foreach (var skipped in result.Skipped)
            text.Append(CultureInfo.InvariantCulture, $"No draft for the {Money.Format(skipped.BudgetCeiling, null)} budget: {skipped.Reason}\n");
        text.Append("A draft is not active until the user accepts it in the app.");
        return text.ToString();
    }

    private static string DescribeDay(PlanDayViewDto view)
    {
        var day = view.Day;
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture,
            $"Day {day.DayIndex} ({view.Date:yyyy-MM-dd}) of \"{view.PlanName}\" (plan {view.PlanId}): ");
        var entries = day.Slots.SelectMany(s => s.Entries).ToList();
        text.Append(entries.Count == 0 ? "no entries" : string.Join("; ", entries.Select(Entry)));
        text.Append(CultureInfo.InvariantCulture,
            $". Totals: {day.Totals.Calories:0} kcal, P {day.Totals.ProteinGrams:0} g, C {day.Totals.CarbsGrams:0} g, F {day.Totals.FatGrams:0} g.");
        foreach (var violation in day.Violations) text.Append(" Violation: ").Append(violation.Message);
        return text.ToString();
    }

    private static string Entry(PlanEntryDto entry)
    {
        var what = entry.Kind switch
        {
            "Recipe" => string.Create(CultureInfo.InvariantCulture, $"{entry.RecipeTitle} x{entry.Servings:0.##}"),
            "Food" => string.Create(CultureInfo.InvariantCulture, $"{entry.FoodName} {entry.Grams:0} g"),
            _ => $"\"{entry.Text}\" (text)",
        };
        return $"{entry.Slot} {entry.Id} {what}";
    }
}
