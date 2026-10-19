using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Prompts;

/// <summary>
/// Starting points a person can pick in their client. Each one is a plain
/// request that names the tools and the rules that matter, so the model begins
/// from the right place. A prompt asks for nothing the connection could not do
/// anyway: the list shows only the prompts whose tools the grant covers.
/// </summary>
[McpServerPromptType]
public sealed class MizanPrompts
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

        For each food, search the catalogue first (search_foods) and log it with log_food. "servings" is a count of the food's own serving size, not grams, so convert my amounts using the serving size in the search result. If a food is not in the catalogue, ask me for the nutrition instead of guessing. Log workouts with log_workout.

        When you are done, show me the diary for that date and tell me anything you were unsure about.
        """;

    [McpServerPrompt(Name = "plan_my_week", Title = "Plan my week")]
    [Description("Build a meal plan for the coming days from your recipes and your goal.")]
    public static string PlanMyWeek(
        [Description("First day of the plan, YYYY-MM-DD. Leave empty to start today.")] string? startDate = null,
        [Description("Anything to respect: foods I dislike, days I eat out, budget, time")] string? preferences = null) =>
        $"""
        Plan my meals in Mizan for the next 7 days{(string.IsNullOrWhiteSpace(startDate) ? "" : $", starting {startDate}")}.

        Look at my recipes (search_recipes) and my current goal (get_current_goal). Create a meal plan (create_meal_plan) and fill it with add_recipe_to_meal_plan, aiming for my daily calories and protein. Use only recipes I have. If I do not have enough, say what is missing instead of inventing recipes.

        {(string.IsNullOrWhiteSpace(preferences) ? "" : $"Please also respect this: {preferences}")}

        Show me the finished plan with its nutrition totals and point out any day that is far from my goal.
        """;

    [McpServerPrompt(Name = "grocery_run", Title = "Grocery run")]
    [Description("Turn a meal plan into a shopping list.")]
    public static string GroceryRun(
        [Description("The meal plan id. Leave empty to use the most recent plan.")] string? mealPlanId = null) =>
        $"""
        Make me a shopping list in Mizan from {(string.IsNullOrWhiteSpace(mealPlanId) ? "my most recent meal plan (list_meal_plans)" : $"meal plan {mealPlanId}")}.

        Read the plan with get_meal_plan and each recipe with get_recipe. Combine the same ingredient across recipes and add up the amounts. Create the list with create_shopping_list and add each item with add_shopping_list_item, with the amount in the name (for example "Rolled oats, 400 g").

        Show me the list grouped by where I would find things in a shop.
        """;

    [McpServerPrompt(Name = "weekly_review", Title = "Weekly review")]
    [Description("Review the past week of eating, training and weight against your goal.")]
    public static string WeeklyReview(
        [Description("Last day of the week, YYYY-MM-DD. Leave empty for today.")] string? weekEnding = null) =>
        $"""
        Review my last 7 days in Mizan{(string.IsNullOrWhiteSpace(weekEnding) ? "" : $", ending {weekEnding}")}.

        Use get_current_goal, get_nutrition_range (days: 7), get_workout_stats, list_body_measurements and get_streak. Base every statement on that data and say when something is missing. A day with nothing logged is unknown, not zero.

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

        Look at the exercises that exist (list_exercises) and my recent training (list_workouts). Use only existing exercises; if one is missing, offer to create it. Build one template per training day with create_workout_template, with sensible sets, reps and rest.

        Show me the plan and explain the split in a few sentences. Do not claim it is medical advice.
        """;

    [McpServerPrompt(Name = "macro_rescue", Title = "What should I eat now?")]
    [Description("Suggest what to eat to finish the day on target.")]
    public static string MacroRescue(
        [Description("The date, YYYY-MM-DD. Leave empty for today.")] string? date = null) =>
        $"""
        I want to finish {(string.IsNullOrWhiteSpace(date) ? "today" : date)} on target in Mizan.

        Read get_nutrition_summary and get_current_goal to see what is left. Then suggest two or three simple options from my own recipes (search_recipes) and foods I have logged before, with the calories and protein each would add. Do not log anything until I choose.
        """;

    [McpServerPrompt(Name = "recipe_from_leftovers", Title = "Recipe from what I have")]
    [Description("Work out what to cook from ingredients you already have.")]
    public static string RecipeFromLeftovers(
        [Description("What is in the fridge or cupboard")] string ingredients) =>
        $"""
        I have these ingredients: {ingredients}

        Suggest one or two meals I can make. Check my own recipes first (search_recipes). For anything new, tell me the method and an estimate of the nutrition, and be clear that it is an estimate. Once I cook and log it, offer to save it as a recipe with promote_to_recipe.
        """;

    [McpServerPrompt(Name = "trainer_checkin", Title = "Client check-in")]
    [Description("Prepare a check-in for a client you coach, from what they shared.")]
    public static string TrainerCheckin(
        [Description("The client's id. Leave empty to choose from your clients.")] string? clientId = null) =>
        $"""
        Prepare a check-in for {(string.IsNullOrWhiteSpace(clientId) ? "one of my clients (list_trainer_clients)" : $"client {clientId}")} in Mizan.

        Use get_client_nutrition. Report only what the client has chosen to share. If an area comes back empty or refused, say it was not shared and do not guess. Then suggest two or three things to talk about.
        """;
}
