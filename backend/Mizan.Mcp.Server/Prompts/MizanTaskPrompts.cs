using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Prompts;

/// <summary>
/// <see cref="MizanPrompts"/> for the task tools (<c>Mcp:Toolset</c> tasks): the same starting points, naming
/// the task tools and their modes. The coach's client check-in has no task tool yet, so it is not offered here.
/// </summary>
[McpServerPromptType]
public sealed class MizanTaskPrompts
{
    [McpServerPrompt(Name = "log_my_day", Title = "Log my day")]
    [Description("Log everything eaten and done on one day from a plain description.")]
    public static string LogMyDay(
        [Description("What you ate, drank and did, in your own words")] string description,
        [Description("The date, YYYY-MM-DD. Leave empty for today.")] string? date = null) =>
        $"""
        Log my day in Mizan{(string.IsNullOrWhiteSpace(date) ? " for today" : $" for {date}")}.

        Here is what I had and did:
        {description}

        Find each food first (find_food, kind food; kind recipe for my saved recipes), then log everything in one log_food call with an item per food. "servings" is a count of the food's own serving size, not grams, so convert my amounts using the serving size find_food returned. If a food is not in the catalogue, ask me for the nutrition instead of guessing. The receipt says what was saved, what failed and what remains; send only what remains. Log workouts with log_workout.

        When you are done, show me the day with get_nutrition (view day, include entries) and tell me anything you were unsure about.
        """;

    [McpServerPrompt(Name = "plan_my_week", Title = "Plan my week")]
    [Description("Build a meal plan for the coming days from your recipes and your goal.")]
    public static string PlanMyWeek(
        [Description("First day of the plan, YYYY-MM-DD. Leave empty to start today.")] string? startDate = null,
        [Description("Anything to respect: foods I dislike, days I eat out, budget, time")] string? preferences = null) =>
        $"""
        Plan my meals in Mizan for the next 7 days{(string.IsNullOrWhiteSpace(startDate) ? ", starting today" : $", starting {startDate}")}.

        Draft it with plan_meals (action generate), passing my words below as the preference, any weekly budget as budgetCeilings and any limit per day as dailyBudget. Mizan picks the foods and recipes against my goal and computes the nutrition and the cost; do not add them up yourself.

        {(string.IsNullOrWhiteSpace(preferences) ? "" : $"My preference: {preferences}")}

        Show me the draft day by day (get_meal_plan, view plan) with its nutrition totals, its weekly cost, the violations it reports and any line it left for me to review. Tell me the draft becomes my plan only when I accept it.
        """;

    [McpServerPrompt(Name = "grocery_run", Title = "Grocery run")]
    [Description("Turn a meal plan into a shopping list.")]
    public static string GroceryRun(
        [Description("The meal plan id. Leave empty to use the most recent plan.")] string? mealPlanId = null) =>
        $"""
        Make me a shopping list in Mizan from {(string.IsNullOrWhiteSpace(mealPlanId) ? "my most recent meal plan (get_meal_plan, view list)" : $"meal plan {mealPlanId}")}.

        Build it with update_shopping_list (action from_plan, planId), which combines the same ingredient across recipes and converts amounts to grams where it can. Do not add up amounts yourself. Then read the list with get_shopping_list (view list, listId).

        Show me the list grouped by prep session (the lines with no session come last), with grams or the amount and unit on each line. Point out every line marked needsReview: it is an amount that could not be converted, so I should check it before shopping. If I confirm a line, fix it with update_shopping_list (action update_item) and set needsReview to false.
        """;

    [McpServerPrompt(Name = "weekly_review", Title = "Weekly review")]
    [Description("Review the past week of eating, training and weight against your goal.")]
    public static string WeeklyReview(
        [Description("Last day of the week, YYYY-MM-DD. Leave empty for today.")] string? weekEnding = null) =>
        $"""
        Review my last 7 days in Mizan{(string.IsNullOrWhiteSpace(weekEnding) ? "" : $", ending {weekEnding}")}.

        Use get_nutrition (view goals, and view range for the 7 days), get_training (view stats), get_body_trend and get_my_account (section achievements, for the streak). Base every statement on that data and say when something is missing. A day with nothing logged is unknown, not zero.

        Give me: what went well, one or two things to change, and one concrete step for next week. Keep it short and kind.
        """;

    [McpServerPrompt(Name = "workout_planner", Title = "Workout planner")]
    [Description("Design a training template for a goal and a schedule.")]
    public static string WorkoutPlanner(
        [Description("What you want, for example build strength or lose fat")] string goal,
        [Description("How many days a week you can train")] int daysPerWeek,
        [Description("Equipment you have, for example dumbbells only")] string? equipment = null) =>
        $"""
        Design a training plan in Mizan. My goal: {goal}. I can train {daysPerWeek} days a week.{(string.IsNullOrWhiteSpace(equipment) ? "" : $" Equipment: {equipment}.")}

        Look at the exercises that exist (get_training, view exercises) and my recent training (get_training, view history). Use only existing exercises; if one is missing, offer to create it (save_workout_template, kind exercise). Build one template per training day with save_workout_template (kind template), with sensible sets, reps and rest.

        Show me the plan and explain the split in a few sentences. Do not claim it is medical advice.
        """;

    [McpServerPrompt(Name = "macro_rescue", Title = "What should I eat now?")]
    [Description("Suggest what to eat to finish the day on target.")]
    public static string MacroRescue(
        [Description("The date, YYYY-MM-DD. Leave empty for today.")] string? date = null) =>
        $"""
        I want to finish {(string.IsNullOrWhiteSpace(date) ? "today" : date)} on target in Mizan.

        Read get_nutrition (view day, include goal) to see what is left. Then rank my saved recipes with find_food (kind fit) and report the ranking as given, and add foods I have logged before, with the calories and protein each would add as Mizan reports them. Do not log anything until I choose.
        """;

    [McpServerPrompt(Name = "recipe_from_leftovers", Title = "Recipe from what I have")]
    [Description("Work out what to cook from ingredients you already have.")]
    public static string RecipeFromLeftovers(
        [Description("What is in the fridge or cupboard")] string ingredients) =>
        $"""
        I have these ingredients: {ingredients}

        Suggest one or two meals I can make. Check my own recipes first (find_food, kind recipe). For anything new, tell me the method and an estimate of the nutrition, and be clear that it is an estimate. Once I cook and log it, offer to save it as a recipe with save_food (kind recipe, fromLog).
        """;
}
