using System.ComponentModel;
using System.Text.Json;
using Mizan.Contracts.Planning;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Extensions.Apps;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

/// <summary>
/// Meal plans, their entries and cook sessions, templates, and the coach's draft
/// for a client. Every tool is a thin call to the planning API (docs/PLANNING.md);
/// nutrition, rule checks and aggregation happen there, and the JSON comes back as is.
/// </summary>
[McpServerToolType]
public sealed class MealPlanTools
{
    private const string RulesHelp = "rulesJson is an object {macroMode?: None|Keto|HighProtein|Custom, targetCalories?, targetProtein?, maxCarbs?, targetFat?, " +
        "eatingWindowStart?: \"HH:mm\", eatingWindowEnd?: \"HH:mm\", allowedFoodIds?: [], forbiddenFoodIds?: [], weeklyBudget?, dailyBudget?, currency?, prepStyle?: Daily|Batch, batchDays?: [dayIndex]}. " +
        "Keto caps carbs at 30 g and takes calories, protein and fat from the user's goal; HighProtein takes calories from the goal and asks for at least 30% of them as protein. " +
        "A typed target always wins; the plan's effectiveTargets show what it is held to, and get_plan_targets previews them.";

    private const string EntriesHelp = "entriesJson is an array of {dayIndex, slot: BREAKFAST|LUNCH|DINNER|SNACK|DRINK|MEAL, kind: Recipe|Food|Text, recipeId?, servings?, foodId?, grams?, text?, notes?, prepSessionIndex?}; " +
        "a Recipe entry needs recipeId and servings, a Food entry foodId and grams, a Text entry text. prepSessionsJson is an array of {dayIndex, title, notes?}, and prepSessionIndex points into it.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IBackendApiClient _api;

    public MealPlanTools(IBackendApiClient api) => _api = api;

    // ---- read ------------------------------------------------------------

    [McpServerTool(Name = "list_meal_plans", ReadOnly = true, Idempotent = true)]
    [Description("List the user's meal plans: their own, their households', and the drafts they authored as a coach. status filters by Draft, Active, Archived or Declined. A generated draft carries generatedFrom.")]
    public async Task<string> ListMealPlans(
        [Description("Status filter: Draft, Active, Archived or Declined (optional)")] string? status = null,
        [Description("Page number (default 1)")] int page = 1,
        [Description("Results per page (default 20)")] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new QueryString().Add("status", status).Add("page", page).Add("pageSize", pageSize);
        return await _api.GetAsync($"/api/MealPlans{query}", ct);
    }

    [McpServerTool(Name = "generate_meal_plan")]
    [Description("Draft a meal plan with the assistant from rules, the user's recipes, catalogue foods and prices. "
        + "One draft per budget in budgetCeilings, each saved with status Draft and returned with its violations and cost table. "
        + "When the catalogue has no foods for some days, nothing is saved and the reply names the missing foods and days. "
        + "Nutrition and cost are computed by Mizan, not written by the model. Spends the daily planning allowance (Free: one plan a day). "
        + "The user confirms a draft in the app, or it can be read with get_meal_plan.")]
    public async Task<string> GenerateMealPlan(
        [Description("First day, YYYY-MM-DD")] string startDate,
        [Description("Number of days, 1 to 14 (default 7)")] int durationDays = 7,
        [Description("What the user wants, in their own words")] string? preference = null,
        [Description("Plan name")] string? name = null,
        [Description("None, Keto, HighProtein or Custom")] string? macroMode = null,
        [Description("Daily calorie target")] decimal? targetCalories = null,
        [Description("Daily protein target in grams")] decimal? targetProtein = null,
        [Description("Daily carb ceiling in grams")] decimal? maxCarbs = null,
        [Description("Weekly budgets to draft against, at most three; one draft each")] decimal[]? budgetCeilings = null,
        [Description("A ceiling for each day's cost, in the budgets' currency; a day over it is a violation")] decimal? dailyBudget = null,
        [Description("Three-letter currency of the budgets; defaults to the user's price region")] string? currency = null,
        CancellationToken ct = default)
    {
        var request = new GenerateMealPlanRequest
        {
            StartDate = ToolArguments.ParseDate(startDate, "startDate"),
            DurationDays = durationDays,
            Preference = preference,
            Name = name,
            Rules = new PlanRulesDto
            {
                MacroMode = string.IsNullOrWhiteSpace(macroMode) ? "None" : macroMode.Trim(),
                TargetCalories = targetCalories,
                TargetProtein = targetProtein,
                MaxCarbs = maxCarbs,
                Currency = string.IsNullOrWhiteSpace(currency) ? null : currency.Trim(),
            },
            BudgetCeilings = budgetCeilings?.ToList() ?? [],
            DailyBudget = dailyBudget,
        };
        try
        {
            return await _api.PostAsync("/api/MealPlans/generate", request, ct);
        }
        catch (BackendApiException error) when (error.ErrorCode == "plan_needs_foods")
        {
            // Not a fault: the catalogue lacks foods for some days, and the message names them.
            return error.Message;
        }
    }

    [McpServerTool(Name = "get_meal_plan", ReadOnly = true, Idempotent = true)]
    [McpAppUi(ResourceUri = Mizan.Mcp.Server.Apps.MizanApps.MealPlan)]
    [Description("Get a meal plan with its days, slots and entries (each with its id, nutrition and whether it is logged), per-day totals, prep sessions, rules, the effective targets the plan is held to, and rule violations.")]
    public async Task<string> GetMealPlan(
        [Description("Meal plan UUID")] string id,
        CancellationToken ct = default)
    {
        return await _api.GetAsync($"/api/MealPlans/{ToolArguments.ParseId(id, "id")}", ct);
    }

    [McpServerTool(Name = "get_plan_targets", ReadOnly = true, Idempotent = true)]
    [Description("Preview the daily targets a macro mode gives the user, with any targets already chosen: each comes back with its value and source (typed, mode, goal or derived), and goalMissing says the mode needs a goal the user has not set.")]
    public async Task<string> GetPlanTargets(
        [Description("None, Keto, HighProtein or Custom")] string? macroMode = null,
        [Description("Daily calories, if chosen")] decimal? targetCalories = null,
        [Description("Daily protein grams, if chosen")] decimal? targetProtein = null,
        [Description("Daily carb ceiling in grams, if chosen")] decimal? maxCarbs = null,
        [Description("Daily fat grams, if chosen")] decimal? targetFat = null,
        CancellationToken ct = default)
    {
        var query = new QueryString()
            .Add("macroMode", string.IsNullOrWhiteSpace(macroMode) ? null : macroMode.Trim())
            .Add("targetCalories", targetCalories?.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Add("targetProtein", targetProtein?.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Add("maxCarbs", maxCarbs?.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Add("targetFat", targetFat?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return await _api.GetAsync($"/api/MealPlans/rules/defaults{query}", ct);
    }

    [McpServerTool(Name = "get_plan_day", ReadOnly = true, Idempotent = true)]
    [McpAppUi(ResourceUri = Mizan.Mcp.Server.Apps.MizanApps.MealPlan)]
    [Description("Get the active plan's day for a date (today by default): its entries with nutrition, totals and violations. Says so in plain text when no active plan covers the date.")]
    public async Task<string> GetPlanDay(
        [Description("Date YYYY-MM-DD (defaults to today)")] string? date = null,
        CancellationToken ct = default)
    {
        var query = new QueryString().Add("date", ToolArguments.ParseOptionalDate(date, "date")?.ToString("yyyy-MM-dd"));
        try
        {
            return await _api.GetAsync($"/api/MealPlans/day{query}", ct);
        }
        catch (BackendApiException error) when (error.Status == System.Net.HttpStatusCode.NotFound && error.ErrorCode == "no_active_plan")
        {
            return "No active plan covers that date.";
        }
    }

    [McpServerTool(Name = "list_templates", ReadOnly = true, Idempotent = true)]
    [Description("List the meal plan templates the user can apply: the global ones and their own.")]
    public async Task<string> ListTemplates(CancellationToken ct = default)
    {
        return await _api.GetAsync("/api/MealPlans/templates", ct);
    }

    // ---- plans -----------------------------------------------------------

    [McpServerTool(Name = "create_meal_plan", Idempotent = false)]
    [Description("Create a meal plan that starts on a date and runs for durationDays (1 to 31). Entries, prep sessions and rules may be supplied at once or added later. " + RulesHelp + " " + EntriesHelp)]
    public async Task<string> CreateMealPlan(
        [Description("Plan name")] string name,
        [Description("First day, YYYY-MM-DD")] string startDate,
        [Description("How many days the plan runs (default 7)")] int durationDays = 7,
        [Description("Description (optional)")] string? description = null,
        [Description("Household UUID to share the plan with (optional)")] string? householdId = null,
        [Description("Optional JSON object with the plan's rules")] string? rulesJson = null,
        [Description("Optional JSON array of entries")] string? entriesJson = null,
        [Description("Optional JSON array of prep sessions")] string? prepSessionsJson = null,
        CancellationToken ct = default)
    {
        var body = CreateBody(name, startDate, durationDays, description, householdId, rulesJson, entriesJson, prepSessionsJson);
        return await _api.PostAsync("/api/MealPlans", body, ct);
    }

    [McpServerTool(Name = "update_meal_plan", Idempotent = true)]
    [Description("Change a plan's name, description, start date, duration or rules. Only the arguments passed change; the rest keep their current values. Shrinking the plan below its last entry's day is refused. " + RulesHelp)]
    public async Task<string> UpdateMealPlan(
        [Description("Meal plan UUID")] string id,
        [Description("New name (optional)")] string? name = null,
        [Description("New description (optional)")] string? description = null,
        [Description("New first day, YYYY-MM-DD (optional)")] string? startDate = null,
        [Description("New duration in days, 1 to 31 (optional)")] int? durationDays = null,
        [Description("New rules as a JSON object; replaces the current rules (optional)")] string? rulesJson = null,
        CancellationToken ct = default)
    {
        var planId = ToolArguments.ParseId(id, "id");
        var current = Parse<MealPlanDetailDto>(await _api.GetAsync($"/api/MealPlans/{planId}", ct), "the plan")
            ?? throw new ArgumentException($"Meal plan {id} was not found.", nameof(id));
        var body = new UpdateMealPlanRequest
        {
            Name = name ?? current.Name,
            Description = description ?? current.Description,
            StartDate = ToolArguments.ParseOptionalDate(startDate, "startDate") ?? current.StartDate,
            DurationDays = durationDays ?? current.DurationDays,
            Rules = ParseJson<PlanRulesDto>(rulesJson, "rulesJson") ?? current.Rules,
        };
        return await _api.PutAsync($"/api/MealPlans/{planId}", body, ct);
    }

    [McpServerTool(Name = "delete_meal_plan", Destructive = true)]
    [Description("Delete a meal plan with all its entries and prep sessions. This is permanent; confirm with the user first.")]
    public async Task<string> DeleteMealPlan(
        [Description("Meal plan UUID")] string id,
        CancellationToken ct = default)
    {
        return await _api.DeleteAsync($"/api/MealPlans/{ToolArguments.ParseId(id, "id")}", ct);
    }

    [McpServerTool(Name = "set_meal_plan_status", Idempotent = true)]
    [Description("Set a plan Active or Archived. Making a personal plan active archives the user's other active personal plan; household plans are left alone.")]
    public async Task<string> SetStatus(
        [Description("Meal plan UUID")] string id,
        [Description("Active or Archived")] string status,
        CancellationToken ct = default)
    {
        return await _api.PostAsync($"/api/MealPlans/{ToolArguments.ParseId(id, "id")}/status", new SetMealPlanStatusRequest(status.Trim()), ct);
    }

    [McpServerTool(Name = "accept_meal_plan", Idempotent = true)]
    [Description("Accept a draft a coach wrote for the user. It becomes their active plan, and their other active personal plan is archived.")]
    public async Task<string> Accept(
        [Description("Meal plan UUID of the draft")] string id,
        CancellationToken ct = default)
    {
        return await _api.PostAsync($"/api/MealPlans/{ToolArguments.ParseId(id, "id")}/accept", null, ct);
    }

    [McpServerTool(Name = "decline_meal_plan", Idempotent = true)]
    [Description("Decline a draft a coach wrote for the user. The coach is told; the draft is kept as declined.")]
    public async Task<string> Decline(
        [Description("Meal plan UUID of the draft")] string id,
        CancellationToken ct = default)
    {
        return await _api.PostAsync($"/api/MealPlans/{ToolArguments.ParseId(id, "id")}/decline", null, ct);
    }

    // ---- entries ---------------------------------------------------------

    [McpServerTool(Name = "add_plan_entry", Idempotent = false)]
    [Description("Add one entry to a plan day and slot. kind is Recipe (recipeId and servings), Food (foodId and grams of the catalogue food) or Text (free text, no nutrition). Returns the new entry's id.")]
    public async Task<string> AddEntry(
        [Description("Meal plan UUID")] string mealPlanId,
        [Description("Day of the plan, 0 is the first day")] int dayIndex,
        [Description("Slot: BREAKFAST, LUNCH, DINNER, SNACK, DRINK or MEAL")] string slot,
        [Description("Recipe, Food or Text")] string kind,
        [Description("Recipe UUID, for a Recipe entry")] string? recipeId = null,
        [Description("Recipe servings, for a Recipe entry")] decimal? servings = null,
        [Description("Food UUID, for a Food entry")] string? foodId = null,
        [Description("Grams of the food, for a Food entry")] decimal? grams = null,
        [Description("The line itself, for a Text entry")] string? text = null,
        [Description("A note shown with the entry (optional)")] string? notes = null,
        [Description("Prep session UUID that cooks this entry (optional)")] string? prepSessionId = null,
        CancellationToken ct = default)
    {
        var body = Entry(dayIndex, slot, kind, recipeId, servings, foodId, grams, text, notes, prepSessionId);
        return await _api.PostAsync($"/api/MealPlans/{ToolArguments.ParseId(mealPlanId, "mealPlanId")}/entries", body, ct);
    }

    [McpServerTool(Name = "update_plan_entry", Idempotent = true)]
    [Description("Replace one entry of a plan. entryId is the entry's own id from get_meal_plan, not the recipe or food id. Send the whole entry: day, slot, kind and the fields that kind needs.")]
    public async Task<string> UpdateEntry(
        [Description("Meal plan UUID")] string mealPlanId,
        [Description("Entry UUID, as listed by get_meal_plan")] string entryId,
        [Description("Day of the plan, 0 is the first day")] int dayIndex,
        [Description("Slot: BREAKFAST, LUNCH, DINNER, SNACK, DRINK or MEAL")] string slot,
        [Description("Recipe, Food or Text")] string kind,
        [Description("Recipe UUID, for a Recipe entry")] string? recipeId = null,
        [Description("Recipe servings, for a Recipe entry")] decimal? servings = null,
        [Description("Food UUID, for a Food entry")] string? foodId = null,
        [Description("Grams of the food, for a Food entry")] decimal? grams = null,
        [Description("The line itself, for a Text entry")] string? text = null,
        [Description("A note shown with the entry (optional)")] string? notes = null,
        [Description("Prep session UUID that cooks this entry (optional)")] string? prepSessionId = null,
        CancellationToken ct = default)
    {
        var body = Entry(dayIndex, slot, kind, recipeId, servings, foodId, grams, text, notes, prepSessionId);
        return await _api.PutAsync($"/api/MealPlans/{ToolArguments.ParseId(mealPlanId, "mealPlanId")}/entries/{ToolArguments.ParseId(entryId, "entryId")}", body, ct);
    }

    [McpServerTool(Name = "remove_plan_entry", Destructive = true)]
    [Description("Remove one entry from a plan. entryId is the ENTRY id from get_meal_plan, not the recipe or food id. Diary rows already logged from it stay.")]
    public async Task<string> RemoveEntry(
        [Description("Meal plan UUID")] string mealPlanId,
        [Description("Entry UUID, as listed by get_meal_plan")] string entryId,
        CancellationToken ct = default)
    {
        return await _api.DeleteAsync($"/api/MealPlans/{ToolArguments.ParseId(mealPlanId, "mealPlanId")}/entries/{ToolArguments.ParseId(entryId, "entryId")}", ct);
    }

    [McpServerTool(Name = "log_plan_entry", Idempotent = false)]
    [Description("Write a plan entry to the user's food diary with the nutrition the plan computed. Date and meal type default to the entry's own day and slot. Calling it again logs the entry again, so check 'logged' on the entry first.")]
    public async Task<string> LogEntry(
        [Description("Meal plan UUID")] string mealPlanId,
        [Description("Entry UUID, as listed by get_meal_plan")] string entryId,
        [Description("Diary date YYYY-MM-DD (defaults to the entry's date)")] string? date = null,
        [Description("Meal type: BREAKFAST, LUNCH, DINNER, SNACK, DRINK or MEAL (defaults to the entry's slot)")] string? mealType = null,
        CancellationToken ct = default)
    {
        var body = new LogPlanEntryRequest
        {
            Date = ToolArguments.ParseOptionalDate(date, "date"),
            MealType = string.IsNullOrWhiteSpace(mealType) ? null : mealType.Trim().ToUpperInvariant(),
        };
        return await _api.PostAsync($"/api/MealPlans/{ToolArguments.ParseId(mealPlanId, "mealPlanId")}/entries/{ToolArguments.ParseId(entryId, "entryId")}/log", body, ct);
    }

    // ---- prep sessions ---------------------------------------------------

    [McpServerTool(Name = "add_prep_session", Idempotent = false)]
    [Description("Add a cooking session to a plan day, for a plan that cooks in batch. Entries point at it with prepSessionId, and the shopping list groups their ingredients under it.")]
    public async Task<string> AddPrepSession(
        [Description("Meal plan UUID")] string mealPlanId,
        [Description("Day of the plan the cooking happens, 0 is the first day")] int dayIndex,
        [Description("Session title, for example 'Sunday batch'")] string title,
        [Description("Notes (optional)")] string? notes = null,
        CancellationToken ct = default)
    {
        return await _api.PostAsync($"/api/MealPlans/{ToolArguments.ParseId(mealPlanId, "mealPlanId")}/prep-sessions",
            new PrepSessionRequest { DayIndex = dayIndex, Title = title, Notes = notes }, ct);
    }

    [McpServerTool(Name = "update_prep_session", Idempotent = true)]
    [Description("Change a prep session's day, title or notes. Send the whole session.")]
    public async Task<string> UpdatePrepSession(
        [Description("Meal plan UUID")] string mealPlanId,
        [Description("Prep session UUID")] string sessionId,
        [Description("Day of the plan the cooking happens, 0 is the first day")] int dayIndex,
        [Description("Session title")] string title,
        [Description("Notes (optional)")] string? notes = null,
        CancellationToken ct = default)
    {
        return await _api.PutAsync($"/api/MealPlans/{ToolArguments.ParseId(mealPlanId, "mealPlanId")}/prep-sessions/{ToolArguments.ParseId(sessionId, "sessionId")}",
            new PrepSessionRequest { DayIndex = dayIndex, Title = title, Notes = notes }, ct);
    }

    [McpServerTool(Name = "remove_prep_session", Destructive = true)]
    [Description("Remove a prep session from a plan. Its entries stay in the plan, unassigned to any session.")]
    public async Task<string> RemovePrepSession(
        [Description("Meal plan UUID")] string mealPlanId,
        [Description("Prep session UUID")] string sessionId,
        CancellationToken ct = default)
    {
        return await _api.DeleteAsync($"/api/MealPlans/{ToolArguments.ParseId(mealPlanId, "mealPlanId")}/prep-sessions/{ToolArguments.ParseId(sessionId, "sessionId")}", ct);
    }

    // ---- shopping list and templates --------------------------------------

    [McpServerTool(Name = "generate_shopping_list", Idempotent = false)]
    [Description("Aggregate a plan's recipe ingredients and food entries into a shopping list, grouped by prep session. Lines that could not be converted to grams are flagged needsReview. Creates a new list, or replaces the items of replaceListId. Returns the list id and counts; read it with get_shopping_list.")]
    public async Task<string> GenerateShoppingList(
        [Description("Meal plan UUID")] string mealPlanId,
        [Description("List name (defaults to the plan's name plus 'shopping list')")] string? name = null,
        [Description("UUID of an existing list whose items are replaced instead of creating a new list (optional)")] string? replaceListId = null,
        CancellationToken ct = default)
    {
        var body = new GenerateShoppingListRequest { Name = name, ReplaceListId = ToolArguments.ParseOptionalId(replaceListId, "replaceListId") };
        return await _api.PostAsync($"/api/MealPlans/{ToolArguments.ParseId(mealPlanId, "mealPlanId")}/shopping-list", body, ct);
    }

    [McpServerTool(Name = "apply_template", Idempotent = false)]
    [Description("Create a plan from a template (list_templates), starting on a date. The new plan copies the template's entries, prep sessions and rules.")]
    public async Task<string> ApplyTemplate(
        [Description("Template UUID")] string templateId,
        [Description("First day of the new plan, YYYY-MM-DD")] string startDate,
        [Description("Household UUID to share the plan with (optional)")] string? householdId = null,
        [Description("Plan name (defaults to the template's name)")] string? name = null,
        CancellationToken ct = default)
    {
        var body = new ApplyTemplateRequest
        {
            StartDate = ToolArguments.ParseDate(startDate, "startDate"),
            HouseholdId = ToolArguments.ParseOptionalId(householdId, "householdId"),
            Name = name,
        };
        return await _api.PostAsync($"/api/MealPlans/templates/{ToolArguments.ParseId(templateId, "templateId")}/apply", body, ct);
    }

    // ---- coaching --------------------------------------------------------

    [McpServerTool(Name = "create_plan_for_client", Idempotent = false)]
    [Description("For a coach whose client allowed meal planning. Draft a meal plan for that client. The plan is the client's, waits as a draft until they accept or decline it, and the client is notified. Takes the same arguments as create_meal_plan, except a household. " + RulesHelp + " " + EntriesHelp)]
    public async Task<string> CreatePlanForClient(
        [Description("Client's user UUID")] string clientId,
        [Description("Plan name")] string name,
        [Description("First day, YYYY-MM-DD")] string startDate,
        [Description("How many days the plan runs (default 7)")] int durationDays = 7,
        [Description("Description (optional)")] string? description = null,
        [Description("Optional JSON object with the plan's rules")] string? rulesJson = null,
        [Description("Optional JSON array of entries")] string? entriesJson = null,
        [Description("Optional JSON array of prep sessions")] string? prepSessionsJson = null,
        CancellationToken ct = default)
    {
        var body = CreateBody(name, startDate, durationDays, description, null, rulesJson, entriesJson, prepSessionsJson);
        return await _api.PostAsync($"/api/Trainers/clients/{ToolArguments.ParseId(clientId, "clientId")}/meal-plans", body, ct);
    }

    [McpServerTool(Name = "apply_template_for_client", Idempotent = false)]
    [Description("For a coach whose client allowed meal planning. Draft the client's meal plan from a template (one of your own or a global one, see list_templates), starting on a date. " +
        "The copy is the client's draft, as with create_plan_for_client: it needs the client to have allowed meal planning, and the client is notified.")]
    public async Task<string> ApplyTemplateForClient(
        [Description("Client's user UUID")] string clientId,
        [Description("Template UUID")] string templateId,
        [Description("First day of the plan, YYYY-MM-DD")] string startDate,
        [Description("Plan name (defaults to the template's name)")] string? name = null,
        CancellationToken ct = default)
    {
        var body = new ApplyTemplateForClientRequest
        {
            TemplateId = ToolArguments.ParseId(templateId, "templateId"),
            StartDate = ToolArguments.ParseDate(startDate, "startDate"),
            Name = name,
        };
        return await _api.PostAsync($"/api/Trainers/clients/{ToolArguments.ParseId(clientId, "clientId")}/meal-plans/from-template", body, ct);
    }

    [McpServerTool(Name = "create_template", Idempotent = false)]
    [Description("For coaches and administrators. Keep a meal plan template of your own, to draft client plans from with apply_template_for_client. Only you see it; a global template is admin_create_template. " + RulesHelp + " " + EntriesHelp)]
    public async Task<string> CreateTemplate(
        [Description("Template name")] string name,
        [Description("How many days the template covers (default 7)")] int durationDays = 7,
        [Description("Description (optional)")] string? description = null,
        [Description("Optional JSON object with the template's rules")] string? rulesJson = null,
        [Description("Optional JSON array of entries")] string? entriesJson = null,
        [Description("Optional JSON array of prep sessions")] string? prepSessionsJson = null,
        CancellationToken ct = default)
        => await _api.PostAsync("/api/MealPlans/templates",
            TemplateBody(name, durationDays, description, global: false, rulesJson, entriesJson, prepSessionsJson), ct);

    [McpServerTool(Name = "update_template", Idempotent = true)]
    [Description("For coaches and administrators. Change one of your own templates: name, description, duration or rules. Its entries are changed with the entry tools, passing the template id as mealPlanId. " + RulesHelp)]
    public async Task<string> UpdateTemplate(
        [Description("Template UUID")] string id,
        [Description("Template name")] string name,
        [Description("How many days the template covers (default 7)")] int durationDays = 7,
        [Description("Description (optional)")] string? description = null,
        [Description("Optional JSON object with the template's rules")] string? rulesJson = null,
        CancellationToken ct = default)
        => await _api.PutAsync($"/api/MealPlans/templates/{ToolArguments.ParseId(id, "id")}",
            UpdateTemplateBody(name, durationDays, description, rulesJson), ct);

    [McpServerTool(Name = "delete_template", Destructive = true)]
    [Description("For coaches and administrators. Delete one of your own templates. Plans already drafted from it are not affected. This is permanent.")]
    public async Task<string> DeleteTemplate(
        [Description("Template UUID")] string id,
        CancellationToken ct = default)
        => await _api.DeleteAsync($"/api/MealPlans/templates/{ToolArguments.ParseId(id, "id")}", ct);

    // ---- administration --------------------------------------------------

    [McpServerTool(Name = "admin_create_template", Idempotent = false)]
    [Description("Admin only. Publish a global meal plan template everyone can apply. " + RulesHelp + " " + EntriesHelp)]
    public async Task<string> AdminCreateTemplate(
        [Description("Template name")] string name,
        [Description("How many days the template covers (default 7)")] int durationDays = 7,
        [Description("Description (optional)")] string? description = null,
        [Description("Optional JSON object with the template's rules")] string? rulesJson = null,
        [Description("Optional JSON array of entries")] string? entriesJson = null,
        [Description("Optional JSON array of prep sessions")] string? prepSessionsJson = null,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/MealPlans/templates",
            TemplateBody(name, durationDays, description, global: true, rulesJson, entriesJson, prepSessionsJson), ct);
    }

    [McpServerTool(Name = "admin_update_template", Idempotent = true)]
    [Description("Admin only. Change a global template's name, description, duration or rules. Its entries are changed with the entry tools, passing the template id as mealPlanId. " + RulesHelp)]
    public async Task<string> AdminUpdateTemplate(
        [Description("Template UUID")] string id,
        [Description("Template name")] string name,
        [Description("How many days the template covers (default 7)")] int durationDays = 7,
        [Description("Description (optional)")] string? description = null,
        [Description("Optional JSON object with the template's rules")] string? rulesJson = null,
        CancellationToken ct = default)
    {
        return await _api.PutAsync($"/api/MealPlans/templates/{ToolArguments.ParseId(id, "id")}",
            UpdateTemplateBody(name, durationDays, description, rulesJson), ct);
    }

    [McpServerTool(Name = "admin_delete_template", Destructive = true)]
    [Description("Admin only. Delete a global template. Plans already created from it are not affected. This is permanent.")]
    public async Task<string> AdminDeleteTemplate(
        [Description("Template UUID")] string id,
        CancellationToken ct = default)
    {
        return await _api.DeleteAsync($"/api/MealPlans/templates/{ToolArguments.ParseId(id, "id")}", ct);
    }

    // ---- helpers ---------------------------------------------------------

    private static CreateTemplateRequest TemplateBody(string name, int durationDays, string? description, bool global,
        string? rulesJson, string? entriesJson, string? prepSessionsJson) => new()
        {
            Name = name,
            Description = description,
            DurationDays = durationDays,
            Global = global,
            Rules = ParseJson<PlanRulesDto>(rulesJson, "rulesJson"),
            Entries = ParseJson<List<PlanEntryRequest>>(entriesJson, "entriesJson") ?? [],
            PrepSessions = ParseJson<List<PrepSessionRequest>>(prepSessionsJson, "prepSessionsJson") ?? [],
        };

    private static UpdateTemplateRequest UpdateTemplateBody(string name, int durationDays, string? description, string? rulesJson) => new()
    {
        Name = name,
        Description = description,
        DurationDays = durationDays,
        Rules = ParseJson<PlanRulesDto>(rulesJson, "rulesJson"),
    };

    private static CreateMealPlanRequest CreateBody(string name, string startDate, int durationDays, string? description, string? householdId,
        string? rulesJson, string? entriesJson, string? prepSessionsJson) => new()
        {
            Name = name,
            Description = description,
            StartDate = ToolArguments.ParseDate(startDate, "startDate"),
            DurationDays = durationDays,
            HouseholdId = ToolArguments.ParseOptionalId(householdId, "householdId"),
            Rules = ParseJson<PlanRulesDto>(rulesJson, "rulesJson"),
            Entries = ParseJson<List<PlanEntryRequest>>(entriesJson, "entriesJson") ?? [],
            PrepSessions = ParseJson<List<PrepSessionRequest>>(prepSessionsJson, "prepSessionsJson") ?? [],
        };

    private static PlanEntryRequest Entry(int dayIndex, string slot, string kind, string? recipeId, decimal? servings, string? foodId, decimal? grams,
        string? text, string? notes, string? prepSessionId) => new()
        {
            DayIndex = dayIndex,
            Slot = slot.Trim().ToUpperInvariant(),
            Kind = kind.Trim(),
            RecipeId = ToolArguments.ParseOptionalId(recipeId, "recipeId"),
            Servings = servings,
            FoodId = ToolArguments.ParseOptionalId(foodId, "foodId"),
            Grams = grams,
            Text = text,
            Notes = notes,
            PrepSessionId = ToolArguments.ParseOptionalId(prepSessionId, "prepSessionId"),
        };

    /// <summary>A JSON argument, or null when it was not supplied. The error names the argument.</summary>
    private static T? ParseJson<T>(string? json, string name) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json, Json) ?? throw new ArgumentException($"{name} must not be JSON null.", name); }
        catch (JsonException error) { throw new ArgumentException($"Invalid {name}: {error.Message}", name); }
    }

    private static T? Parse<T>(string json, string what) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(json, Json); }
        catch (JsonException error) { throw new InvalidOperationException($"The API returned {what} in an unexpected shape: {error.Message}"); }
    }
}
