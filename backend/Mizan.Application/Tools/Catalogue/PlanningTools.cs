using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Mizan.Application.Ai;
using Mizan.Application.Ai.Tools;
using Mizan.Application.Exceptions;
using Mizan.Application.Notifications;
using Mizan.Application.Planning;
using Mizan.Contracts.Ai;
using Mizan.Contracts.Mcp;
using Mizan.Contracts.Notifications;
using Mizan.Contracts.Planning;
using Mizan.Domain.Ai;

namespace Mizan.Application.Tools.Catalogue;

/// <summary>
/// Meal plans, shopping lists and reminders (docs/PLANNING.md). An outside agent may name a
/// household on a new plan or list; the assistant never does, so what it makes is the person's own.
/// The assistant changes only drafts: it adds and rewrites a draft's entries and removes them.
/// </summary>
public static class PlanningTools
{
    public static IReadOnlyList<TaskTool> All { get; } =
    [
        new TaskTool
        {
            Name = "get_meal_plan",
            Title = "Meal plans",
            Description = "The person's meal plans: the list, one plan with its days, entries (each with its entry id), prep sessions and violations, "
                + "one day of a plan, the targets a macro mode gives, or the templates a plan can start from. The plan and day views render the meal-plan view in hosts that show apps.",
            Kind = ToolKind.Read,
            ModeArgument = "view",
            DefaultMode = "list",
            App = "ui://mizan/meal-plan.html",
            Properties = new JsonObject
            {
                ["planId"] = ToolSchema.Id("plan: the plan. day: a plan (a draft too); without it, the active plan on that date."),
                ["fromDay"] = ToolSchema.Integer($"plan: the first day to return, from 0; {PlanWindowDays} days come back at a time."),
                ["date"] = ToolSchema.Date("day: YYYY-MM-DD, today by default (with planId, the plan's first day)."),
                ["status"] = ToolSchema.Enum("list: only plans in this status.", ["Draft", "Active", "Archived", "Declined"]),
                ["householdId"] = ToolSchema.Id("list: only the plans shared in this household (get_household view mine)."),
                ["page"] = ToolSchema.Integer("list: page, from 1."),
                ["pageSize"] = ToolSchema.Integer("list: plans per page, 20 by default."),
                ["macroMode"] = ToolSchema.Enum("targets: the macro mode to preview.", ["None", "Keto", "HighProtein", "Custom"]),
                ["targetCalories"] = ToolSchema.Number("targets: daily calories, if chosen."),
                ["targetProtein"] = ToolSchema.Number("targets: daily protein in g, if chosen."),
                ["maxCarbs"] = ToolSchema.Number("targets: daily carb ceiling in g, if chosen."),
                ["targetFat"] = ToolSchema.Number("targets: daily fat in g, if chosen."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "list",
                    Summary = "The person's plans, their households' and the drafts a coach wrote for them; filter by status.",
                    Area = McpScopes.PlanningRead,
                    Requests = [typeof(GetMealPlansQuery)],
                    Replaces = ["list_meal_plans"],
                    Build = (args, context) => new GetMealPlansQuery
                    {
                        Status = args.OptionalString("status"),
                        // An outside agent names the household; the assistant stays in the active one.
                        HouseholdId = context.Surface == ToolSurface.Mcp ? args.OptionalGuid("householdId") : null,
                        Page = args.OptionalInt("page") ?? 1,
                        PageSize = Math.Clamp(args.OptionalInt("pageSize") ?? 20, 1, 100),
                    },
                },
                new ToolMode
                {
                    Name = "plan",
                    Summary = "One plan by planId: days, slots and entries with their ids, totals, prep sessions, rules, targets and violations, four days at a time from fromDay.",
                    Area = McpScopes.PlanningRead,
                    Surfaces = ToolSurface.Both,
                    Requests = [typeof(GetMealPlanByIdQuery)],
                    Replaces = ["get_meal_plan"],
                    Build = (args, _) => new GetMealPlanByIdQuery(args.RequiredGuid("planId")),
                    Window = (result, args) => result is MealPlanDetailDto plan ? PlanWindow(plan, args.OptionalInt("fromDay") ?? 0) : result,
                    NotFound = "There is no such plan, or it is not yours to read.",
                    Describe = result => result is MealPlanDetailDto plan ? DescribePlan(plan) : "There is no such plan.",
                },
                new ToolMode
                {
                    Name = "day",
                    Summary = "One day of a plan with its entries and their ids, totals and violations: the active plan on date, or planId's.",
                    Area = McpScopes.PlanningRead,
                    Surfaces = ToolSurface.Both,
                    Requests = [typeof(GetPlanDayQuery)],
                    Replaces = ["get_plan_day"],
                    Build = (args, _) => new GetPlanDayQuery(args.OptionalDate("date"), args.OptionalGuid("planId")),
                    Shape = result => result ?? new { day = (object?)null, message = "No active plan covers that date. Drafts are not active until accepted; pass planId to read one." },
                    Describe = result => result is PlanDayViewDto day ? AiPlanningTools.DescribeDay(day) : "No active plan covers that day. Drafts are not active until the person accepts them: list the plans to find one, then ask for its day with planId.",
                    Cards = result => result is PlanDayViewDto day
                        ? [new AiCardDraft(AiCardTypes.DayPlan, new DayPlanCardData(day.PlanId, day.PlanName, day.Day))]
                        : [],
                },
                new ToolMode
                {
                    Name = "targets",
                    Summary = "The daily targets a macro mode gives the person, each with its source; goalMissing says the mode needs a goal.",
                    Area = McpScopes.PlanningRead,
                    Requests = [typeof(GetPlanTargetsQuery)],
                    Replaces = ["get_plan_targets"],
                    Build = (args, _) => new GetPlanTargetsQuery(
                        args.OptionalString("macroMode"),
                        args.OptionalDecimal("targetCalories"),
                        args.OptionalDecimal("targetProtein"),
                        args.OptionalDecimal("maxCarbs"),
                        args.OptionalDecimal("targetFat")),
                },
                new ToolMode
                {
                    Name = "templates",
                    Summary = "The templates a plan can start from: the global ones and the person's own.",
                    Area = McpScopes.PlanningRead,
                    Requests = [typeof(GetTemplatesQuery)],
                    Replaces = ["list_templates"],
                    Build = (_, _) => new GetTemplatesQuery(),
                },
            ],
        },
        new TaskTool
        {
            Name = "plan_meals",
            Title = "Make or change a meal plan",
            Description = "Make a meal plan (drafted by Mizan's AI against rules and budgets, empty, or from a template), change its details, "
                + "make it active or archive it, accept or decline a coach's draft, or suggest meals for the rest of today. Nutrition and cost are computed by Mizan. "
                + "A drafted plan is not followed until the person accepts it. Entries are changed with edit_meal_plan.",
            Kind = ToolKind.Write,
            ModeArgument = "action",
            Properties = new JsonObject
            {
                ["planId"] = ToolSchema.Id("update, activate, archive, accept, decline: the plan."),
                ["name"] = ToolSchema.String("generate, create, update: the plan's name."),
                ["description"] = ToolSchema.String("create, update: a description."),
                ["startDate"] = ToolSchema.Date("generate, create, update: the first day, YYYY-MM-DD. Ask the person which day the plan starts when they have not said; never assume one."),
                ["durationDays"] = ToolSchema.Integer("generate: 1 to 14 days; create, update: 1 to 31. 7 by default."),
                ["preference"] = ToolSchema.String("generate: what the person asked for, in their words."),
                ["budgetCeilings"] = ToolSchema.Array("generate: weekly budgets, at most three; one draft each.", new JsonObject { ["type"] = "number" }),
                ["dailyBudget"] = ToolSchema.Number("generate: a ceiling for each day's cost, in the budgets' currency."),
                ["macroMode"] = ToolSchema.Enum("generate: the macro mode.", ["None", "Keto", "HighProtein", "Custom"]),
                ["targetCalories"] = ToolSchema.Number("generate: daily calories."),
                ["targetProtein"] = ToolSchema.Number("generate: daily protein in g."),
                ["maxCarbs"] = ToolSchema.Number("generate: daily carb ceiling in g."),
                ["targetFat"] = ToolSchema.Number("generate: daily fat in g."),
                ["weeklyBudget"] = ToolSchema.Number("generate: the weekly budget."),
                ["currency"] = ToolSchema.String("generate: three letters, such as ETB; the person's price region's by default."),
                ["prepStyle"] = ToolSchema.Enum("generate: cook daily or in batch.", ["Daily", "Batch"]),
                ["rules"] = ToolSchema.Object("generate, create, update: the plan's whole rules (macroMode, targetCalories, targetProtein, maxCarbs, targetFat, "
                    + "eatingWindowStart, eatingWindowEnd, allowedFoodIds, forbiddenFoodIds, weeklyBudget, dailyBudget, currency, prepStyle, batchDays). "
                    + "On update it replaces the current rules."),
                ["entries"] = ToolSchema.Array("create: entries to start with (dayIndex from 0, slot, kind Recipe/Food/Text, recipeId and servings, foodId and grams, or text; "
                    + "prepSessionIndex points into prepSessions).", new JsonObject { ["type"] = "object" }),
                ["prepSessions"] = ToolSchema.Array("create: cooking sessions (dayIndex, title, notes).", new JsonObject { ["type"] = "object" }),
                ["templateId"] = ToolSchema.Id("create: start from this template (get_meal_plan view templates), copying its entries, prep sessions and rules."),
                ["householdId"] = ToolSchema.Id("create: share the plan with this household. Optional."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "generate",
                    Summary = "Draft a plan from the rules, the person's recipes, catalogue foods and prices: one draft per budget. Spends the AI allowance.",
                    Area = McpScopes.PlanningWrite,
                    Surfaces = ToolSurface.Both,
                    Requests = [typeof(GenerateMealPlanCommand)],
                    Replaces = ["generate_meal_plan"],
                    Build = (args, _) => new GenerateMealPlanCommand
                    {
                        StartDate = args.RequiredDate("startDate"),
                        DurationDays = args.OptionalInt("durationDays") ?? 7,
                        Preference = args.OptionalString("preference"),
                        Name = args.OptionalString("name"),
                        Rules = args.As<PlanRulesDto>("rules") ?? AiToolArgs.Planning.Rules(args.Root),
                        BudgetCeilings = AiToolArgs.DecimalList(args.Root, "budgetCeilings"),
                        DailyBudget = args.OptionalDecimal("dailyBudget"),
                    },
                    Describe = result => result is GenerateMealPlanResult generated ? AiPlanningTools.DescribeGenerated(generated) : "Drafted a plan.",
                    Cards = result => result is GenerateMealPlanResult generated
                        ? generated.Drafts.SelectMany(draft => new[]
                        {
                            new AiCardDraft(AiCardTypes.PlanPreview, AiPlanningTools.Preview(draft)),
                            new AiCardDraft(AiCardTypes.CostTable, new CostTableCardData(draft.Plan.Id, draft.Cost)),
                        }).ToList()
                        : [],
                },
                new ToolMode
                {
                    Name = "create",
                    Summary = "A plan from startDate for durationDays, empty or with entries, or from templateId. It becomes the person's active plan.",
                    Area = McpScopes.PlanningWrite,
                    Surfaces = ToolSurface.Both,
                    Axis = DataAxis.Nutrition,
                    Requests = [typeof(CreateMealPlanCommand), typeof(ApplyTemplateCommand)],
                    Replaces = ["create_meal_plan", "apply_template"],
                    Build = CreatePlan,
                    Describe = result => result is MealPlanCreatedResult plan && !string.IsNullOrWhiteSpace(plan.Name)
                        ? $"Created the plan \"{plan.Name}\" (plan {plan.Id})."
                        : "Created your meal plan.",
                },
                new ToolMode
                {
                    Name = "update",
                    Summary = "Change planId's name, description, start, duration or rules; what is left out keeps its value.",
                    Area = McpScopes.PlanningWrite,
                    Requests = [typeof(PatchMealPlanCommand)],
                    Replaces = ["update_meal_plan"],
                    Build = (args, _) => new PatchMealPlanCommand(
                        args.RequiredGuid("planId"),
                        args.OptionalString("name"),
                        args.OptionalString("description"),
                        args.OptionalDate("startDate"),
                        args.OptionalInt("durationDays"),
                        args.As<PlanRulesDto>("rules")),
                    Shape = _ => new { updated = true },
                },
                StatusMode("activate", "Make planId the plan the person follows; their other active personal plan is archived.", "Active"),
                StatusMode("archive", "Archive planId.", "Archived"),
                new ToolMode
                {
                    Name = "accept",
                    Summary = "Accept a coach's draft, planId: it becomes the person's active plan.",
                    Area = McpScopes.PlanningWrite,
                    Requests = [typeof(AcceptPlanCommand)],
                    Replaces = ["accept_meal_plan"],
                    Build = (args, _) => new AcceptPlanCommand(args.RequiredGuid("planId")),
                    Shape = _ => new { accepted = true },
                },
                new ToolMode
                {
                    Name = "decline",
                    Summary = "Decline a coach's draft, planId. The coach is told; the draft is kept as declined.",
                    Area = McpScopes.PlanningWrite,
                    Requests = [typeof(DeclinePlanCommand)],
                    Replaces = ["decline_meal_plan"],
                    Build = (args, _) => new DeclinePlanCommand(args.RequiredGuid("planId")),
                    Shape = _ => new { declined = true },
                },
                new ToolMode
                {
                    Name = "suggest_now",
                    Summary = "Meal ideas for the rest of today against what is left of the targets. Spends the AI allowance.",
                    Area = McpScopes.PlanningWrite,
                    Requests = [typeof(SuggestMealsCommand)],
                    Replaces = ["suggest_meals"],
                    Build = (_, _) => new SuggestMealsCommand(),
                },
            ],
        },
        new TaskTool
        {
            Name = "edit_meal_plan",
            Title = "Edit a meal plan",
            Description = "Add or rewrite a plan's entries and prep sessions in one call. Changes are made in order and are not one transaction: "
                + "the receipt says what completed, what failed and what remains, so send only what remains. An entry is a recipe (recipeId, servings), "
                + "a catalogue food (foodId, grams) or a plain text line. Remove one with delete.",
            Kind = ToolKind.Write,
            Required = ["planId", "changes"],
            Properties = new JsonObject
            {
                ["planId"] = ToolSchema.Id("The plan, or a template of your own."),
                ["changes"] = ToolSchema.Array("The changes, in order.", ToolSchema.Object("One change.", new JsonObject
                {
                    ["action"] = ToolSchema.Enum("What to do.", EditMealPlanCommand.Actions),
                    ["entryId"] = ToolSchema.Id("update_entry: the entry's own id from get_meal_plan, not the recipe or food id."),
                    ["sessionId"] = ToolSchema.Id("update_prep: the prep session."),
                    ["dayIndex"] = ToolSchema.Integer("0 is the plan's first day."),
                    ["slot"] = ToolSchema.Enum("Entries: the slot.", ["BREAKFAST", "LUNCH", "DINNER", "SNACK", "DRINK", "MEAL"]),
                    ["recipeId"] = ToolSchema.Id("Entries: one of the person's recipes; makes a recipe entry."),
                    ["servings"] = ToolSchema.Number("Entries: recipe servings, 1 by default."),
                    ["foodId"] = ToolSchema.Id("Entries: a catalogue food from find_food; makes a food entry."),
                    ["grams"] = ToolSchema.Number("Entries: grams of the food."),
                    ["text"] = ToolSchema.String("Entries: a plain line when there is no recipe or food."),
                    ["prepSessionId"] = ToolSchema.Id("Entries: the prep session that cooks it. Optional."),
                    ["title"] = ToolSchema.String("Prep: the session's title, such as 'Sunday batch'."),
                    ["notes"] = ToolSchema.String("A note. Optional."),
                }, "action")),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "changes",
                    Summary = "Make the changes; returns what was saved, what failed and what remains.",
                    Area = McpScopes.PlanningWrite,
                    Surfaces = ToolSurface.Both,
                    Requests = [typeof(EditMealPlanCommand)],
                    Replaces = ["add_plan_entry", "update_plan_entry", "add_prep_session", "update_prep_session"],
                    Build = (args, context) => new EditMealPlanCommand(
                        args.RequiredGuid("planId"),
                        args.Objects("changes").Select(Change).ToList(),
                        // The assistant changes a draft only: the person reviews it before following it.
                        DraftOnly: context.Surface == ToolSurface.Assistant),
                    Describe = result => result is PlanningBatchReceipt receipt ? ReceiptText(receipt, "change") : "Changed the plan.",
                },
            ],
        },
        new TaskTool
        {
            Name = "get_shopping_list",
            Title = "Shopping lists",
            Description = "The person's shopping lists with their counts and the plan each came from, or one list with its items: "
                + "amount or grams, category, ticked, needsReview, and the prep session each belongs to.",
            Kind = ToolKind.Read,
            ModeArgument = "view",
            DefaultMode = "lists",
            Properties = new JsonObject
            {
                ["listId"] = ToolSchema.Id("list: the shopping list."),
                ["householdId"] = ToolSchema.Id("lists: only the lists shared in this household (get_household view mine)."),
                ["page"] = ToolSchema.Integer("lists: page, from 1."),
                ["pageSize"] = ToolSchema.Integer("lists: lists per page, 20 by default."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "lists",
                    Summary = "Every list the person can see, with item, ticked and needs-review counts.",
                    Area = McpScopes.PlanningRead,
                    Requests = [typeof(GetShoppingListsQuery)],
                    Replaces = ["list_shopping_lists"],
                    Build = (args, context) => new GetShoppingListsQuery
                    {
                        HouseholdId = context.Surface == ToolSurface.Mcp ? args.OptionalGuid("householdId") : null,
                        Page = args.OptionalInt("page") ?? 1,
                        PageSize = Math.Clamp(args.OptionalInt("pageSize") ?? 20, 1, 100),
                    },
                },
                new ToolMode
                {
                    Name = "list",
                    Summary = "One list, listId, with its items in prep-session and sort order.",
                    Area = McpScopes.PlanningRead,
                    Requests = [typeof(GetShoppingListQuery)],
                    Replaces = ["get_shopping_list"],
                    Build = (args, _) => new GetShoppingListQuery(args.RequiredGuid("listId")),
                    NotFound = "There is no such shopping list, or it is not yours to read.",
                },
            ],
        },
        new TaskTool
        {
            Name = "update_shopping_list",
            Title = "Change a shopping list",
            Description = "Start a shopping list, make one from a meal plan (Mizan adds up the amounts and marks lines it cannot weigh for review), "
                + "rename it, add items, change an item, or tick one off. Remove items or a list with delete.",
            Kind = ToolKind.Write,
            ModeArgument = "action",
            Properties = new JsonObject
            {
                ["listId"] = ToolSchema.Id("rename, add_items: the list."),
                ["name"] = ToolSchema.String("create, rename: the list's name. from_plan: optional, the plan's name by default."),
                ["planId"] = ToolSchema.Id("from_plan: the meal plan."),
                ["replaceListId"] = ToolSchema.Id("from_plan: a list whose items are replaced instead of making a new list. Optional."),
                ["householdId"] = ToolSchema.Id("create: share the list with this household. Optional."),
                ["items"] = ToolSchema.Array("add_items: the items.", ToolSchema.Object("One item.", new JsonObject
                {
                    ["itemName"] = ToolSchema.String("What to buy."),
                    ["amount"] = ToolSchema.Number("An amount, when it is not weighed."),
                    ["unit"] = ToolSchema.String("The amount's unit, such as pcs or bunch."),
                    ["grams"] = ToolSchema.Number("The weight in grams, when known."),
                    ["category"] = ToolSchema.String("A category, such as produce or dairy."),
                    ["foodId"] = ToolSchema.Id("The catalogue food the line is for. Optional."),
                    ["prepSessionId"] = ToolSchema.Id("The plan's prep session the line belongs to. Optional."),
                }, "itemName")),
                ["itemId"] = ToolSchema.Id("update_item, check: the item."),
                ["itemName"] = ToolSchema.String("update_item: a new name."),
                ["amount"] = ToolSchema.Number("update_item: a new amount."),
                ["unit"] = ToolSchema.String("update_item: a new unit."),
                ["grams"] = ToolSchema.Number("update_item: a new weight in grams."),
                ["category"] = ToolSchema.String("update_item: a new category."),
                ["needsReview"] = ToolSchema.Boolean("update_item: false once the amount has been checked."),
                ["sortOrder"] = ToolSchema.Integer("update_item: its position in the list."),
                ["isChecked"] = ToolSchema.Boolean("check: true when bought (the default), false to untick. update_item: the same."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "create",
                    Summary = "An empty list called name. Items are added with add_items.",
                    Area = McpScopes.PlanningWrite,
                    Surfaces = ToolSurface.Both,
                    Axis = DataAxis.Nutrition,
                    Requests = [typeof(CreateShoppingListCommand)],
                    Replaces = ["create_shopping_list"],
                    Build = (args, context) => new CreateShoppingListCommand(
                        args.RequiredString("name"),
                        // Never from the assistant: a household it guessed would be someone else's.
                        context.Surface == ToolSurface.Mcp ? args.OptionalGuid("householdId") : null),
                    Shape = result => result is Guid id ? new { listId = id } : result,
                    Describe = result => result is Guid id ? $"Created the shopping list {id}." : "Created that shopping list.",
                },
                new ToolMode
                {
                    Name = "from_plan",
                    Summary = "A list made from planId's recipes and foods, grouped by prep session; or replaceListId's items replaced.",
                    Area = McpScopes.PlanningWrite,
                    Surfaces = ToolSurface.Both,
                    Requests = [typeof(GenerateShoppingListCommand)],
                    Replaces = ["generate_shopping_list"],
                    Build = (args, context) => new GenerateShoppingListCommand(
                        args.RequiredGuid("planId"),
                        args.OptionalString("name"),
                        context.Surface == ToolSurface.Mcp ? args.OptionalGuid("replaceListId") : null),
                    Describe = result => result is GenerateShoppingListResult list
                        ? $"Made shopping list {list.ListId} with {list.ItemCount} item(s), {list.NeedsReviewCount} to review."
                        : "Made the shopping list.",
                    Cards = result => result is GenerateShoppingListResult list
                        ? [new AiCardDraft(AiCardTypes.ShoppingList, new ShoppingListCardData(list.ListId, list.ItemCount, list.NeedsReviewCount))]
                        : [],
                },
                new ToolMode
                {
                    Name = "rename",
                    Summary = "Rename listId to name.",
                    Area = McpScopes.PlanningWrite,
                    Requests = [typeof(UpdateShoppingListCommand)],
                    Replaces = ["update_shopping_list"],
                    Build = (args, _) => new UpdateShoppingListCommand(args.RequiredGuid("listId"), args.RequiredString("name")),
                    Shape = _ => new { renamed = true },
                },
                new ToolMode
                {
                    Name = "add_items",
                    Summary = "Add the items to listId; returns what was added and what remains.",
                    Area = McpScopes.PlanningWrite,
                    Surfaces = ToolSurface.Both,
                    Axis = DataAxis.Nutrition,
                    Requests = [typeof(AddShoppingListItemsCommand)],
                    Replaces = ["add_shopping_list_item"],
                    Build = (args, _) => new AddShoppingListItemsCommand(
                        args.RequiredGuid("listId"),
                        args.As<List<AddShoppingListItemRequest>>("items") ?? throw new DomainValidationException("'items' is required: a list of what to buy.")),
                    Describe = result => result is PlanningBatchReceipt receipt ? ReceiptText(receipt, "item") : "Added that to the list.",
                },
                new ToolMode
                {
                    Name = "update_item",
                    Summary = "Change itemId; only the fields given change.",
                    Area = McpScopes.PlanningWrite,
                    Requests = [typeof(UpdateShoppingListItemCommand)],
                    Replaces = ["update_shopping_list_item"],
                    Build = (args, _) => new UpdateShoppingListItemCommand(args.RequiredGuid("itemId"), new UpdateShoppingListItemRequest
                    {
                        ItemName = args.OptionalString("itemName"),
                        Amount = args.OptionalDecimal("amount"),
                        Unit = args.OptionalString("unit"),
                        Grams = args.OptionalDecimal("grams"),
                        Category = args.OptionalString("category"),
                        IsChecked = args.OptionalBool("isChecked"),
                        NeedsReview = args.OptionalBool("needsReview"),
                        SortOrder = args.OptionalInt("sortOrder"),
                    }),
                    Shape = _ => new { updated = true },
                },
                new ToolMode
                {
                    Name = "check",
                    Summary = "Tick itemId as bought, or untick it with isChecked false.",
                    Area = McpScopes.PlanningWrite,
                    Requests = [typeof(ToggleShoppingListItemCommand)],
                    Replaces = ["toggle_shopping_list_item"],
                    Build = (args, _) => new ToggleShoppingListItemCommand(args.RequiredGuid("itemId"), args.OptionalBool("isChecked") ?? true),
                    Shape = _ => new { updated = true },
                },
            ],
        },
        new TaskTool
        {
            Name = "set_reminder",
            Title = "Set a reminder",
            Description = ReminderToolGuidance.CreateReminder
                + " Reminders repeat at most once an hour, quiet hours still apply, and the plan caps how many are on. "
                + "An existing reminder can be changed, marked done, snoozed or skipped for today; list them with get_my_account.",
            Kind = ToolKind.Write,
            ModeArgument = "action",
            DefaultMode = "create",
            Properties = new JsonObject
            {
                ["id"] = ToolSchema.Id("update, done, snooze, skip: the reminder."),
                ["kind"] = ToolSchema.Enum("create: what it is about.", ReminderKinds),
                ["title"] = ToolSchema.String("create, update: what it says. Each kind but custom has a default."),
                ["note"] = ToolSchema.String("create, update: a line under the title."),
                ["repeat"] = ToolSchema.Enum("create, update: the schedule. days needs days; every needs exactly one of everyHours or everyDays (every other day is every with everyDays 2); "
                    + "monthly needs dayOfMonth, or week with exactly one day. Every repeat except everyHours needs a time.", ["once", "daily", "days", "every", "monthly"]),
                ["time"] = ToolSchema.String("HH:mm, such as 07:00."),
                ["days"] = ToolSchema.Array("The weekdays of a days repeat, or the one weekday of a monthly repeat with week.",
                    ToolSchema.Enum("A weekday.", ["mon", "tue", "wed", "thu", "fri", "sat", "sun"])),
                ["dayOfMonth"] = ToolSchema.Integer("monthly on a date, 1 to 31; 31 is the last day of every month."),
                ["week"] = ToolSchema.Enum("monthly on a weekday: which one of the month.", ["first", "second", "third", "fourth", "last"]),
                ["date"] = ToolSchema.Date("The day of a once, or the first day of an every."),
                ["everyHours"] = ToolSchema.Integer("every: hours between."),
                ["everyDays"] = ToolSchema.Integer("every: days between."),
                ["mealType"] = ToolSchema.Enum("create, meal_log: the meal.", ["BREAKFAST", "LUNCH", "DINNER", "SNACK"]),
                ["workoutTemplateId"] = ToolSchema.Id("create, workout: the template to train."),
                ["mealPlanId"] = ToolSchema.Id("create, pre_cook: the meal plan."),
                ["prepSessionId"] = ToolSchema.Id("create, pre_cook: the plan's prep session."),
                ["prepTime"] = ToolSchema.String("create, update, pre_cook: when the prep starts, HH:mm; 18:00 by default."),
                ["leadMinutes"] = ToolSchema.Integer("create, update, pre_cook: minutes before the prep; 60 by default."),
                ["channels"] = ToolSchema.Array("create, update: inApp, telegram, push, email. All by default; the person's preferences still apply.",
                    ToolSchema.Enum("A channel.", ["inApp", "telegram", "push", "email"])),
                ["enabled"] = ToolSchema.Boolean("update: false turns it off and keeps it; true turns it back on."),
                ["minutes"] = ToolSchema.Integer("snooze: minutes to wait, 30 by default."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "create",
                    Summary = "A new reminder of kind, on the schedule given.",
                    Area = McpScopes.PlanningWrite,
                    Surfaces = ToolSurface.Both,
                    Axis = DataAxis.Planning,
                    Requests = [typeof(CreateReminderCommand)],
                    Replaces = ["create_reminder"],
                    Build = (args, context) =>
                    {
                        var request = AiReminderTools.Request(args.Root);
                        return new CreateReminderCommand(context.Surface == ToolSurface.Mcp && args.Has("channels")
                            ? request with { Channels = args.StringList("channels") }
                            : request);
                    },
                    Describe = result => result is ReminderDto reminder
                        ? $"Set the reminder \"{reminder.Title}\" ({reminder.Id}): {reminder.Summary}." + (reminder.NextRunText is { } next ? $" First one {next}." : "")
                        : "Set that reminder.",
                },
                new ToolMode
                {
                    Name = "update",
                    Summary = "Change reminder id; a field left out keeps its value. A pre_cook reminder changes through prepTime and leadMinutes.",
                    Area = McpScopes.PlanningWrite,
                    Requests = [typeof(UpdateReminderCommand)],
                    Replaces = ["update_reminder"],
                    Build = (args, _) =>
                    {
                        var schedule = args.Has("repeat") ? AiReminderTools.Request(WithKind(args)).Schedule : null;
                        return new UpdateReminderCommand(args.RequiredGuid("id"), new UpdateReminderRequest(
                            args.OptionalString("title"),
                            args.OptionalString("note"),
                            schedule,
                            args.Has("channels") ? args.StringList("channels") : null,
                            args.OptionalBool("enabled"),
                            args.OptionalString("prepTime"),
                            args.OptionalInt("leadMinutes")));
                    },
                },
                new ToolMode
                {
                    Name = "done",
                    Summary = "Mark reminder id done: the rest of today's deliveries are dropped, and a one-off is finished.",
                    Area = McpScopes.PlanningWrite,
                    Requests = [typeof(CompleteReminderCommand)],
                    Replaces = ["complete_reminder"],
                    Build = (args, _) => new CompleteReminderCommand(args.RequiredGuid("id")),
                },
                new ToolMode
                {
                    Name = "snooze",
                    Summary = "Snooze reminder id for minutes; the answer says when it fires again.",
                    Area = McpScopes.PlanningWrite,
                    Requests = [typeof(SnoozeReminderCommand)],
                    Replaces = ["snooze_reminder"],
                    Build = (args, _) => new SnoozeReminderCommand(args.RequiredGuid("id"), args.OptionalInt("minutes")),
                },
                new ToolMode
                {
                    Name = "skip",
                    Summary = "Skip reminder id for today; it stays on for later days.",
                    Area = McpScopes.PlanningWrite,
                    Requests = [typeof(SkipReminderCommand)],
                    Replaces = ["skip_reminder"],
                    Build = (args, _) => new SkipReminderCommand(args.RequiredGuid("id")),
                },
            ],
        },
    ];

    public static IReadOnlyList<ToolMode> DeleteKinds { get; } =
    [
        new ToolMode
        {
            Name = "meal_plan",
            Summary = "A meal plan with its entries and prep sessions, by id. Previews first: its name, status, counts and the lists made from it.",
            Area = McpScopes.PlanningWrite,
            Requests = [typeof(DeleteMealPlanCommand)],
            Replaces = ["delete_meal_plan"],
            Build = (args, _) => new DeleteMealPlanCommand(args.RequiredGuid("id")),
            Shape = _ => new { deleted = true },
            Confirm = new ToolConfirmStep(
                typeof(GetMealPlanDeletePreviewQuery),
                (args, _) => new GetMealPlanDeletePreviewQuery(args.RequiredGuid("id")),
                result => MealPlanPreviewFacts(result as MealPlanDeletePreview)),
        },
        new ToolMode
        {
            Name = "plan_entry",
            Summary = "One entry of planId, by its entry id. Diary rows already logged from it stay. The assistant removes entries of a draft only.",
            Area = McpScopes.PlanningWrite,
            Surfaces = ToolSurface.Both,
            Requests = [typeof(RemovePlanEntryCommand)],
            Replaces = ["remove_plan_entry"],
            Build = (args, context) => new RemovePlanEntryCommand(
                args.RequiredGuid("planId"),
                args.RequiredGuid("id"),
                DraftOnly: context.Surface == ToolSurface.Assistant),
            Shape = _ => new { removed = true },
            Describe = _ => "Removed that entry.",
        },
        new ToolMode
        {
            Name = "prep_session",
            Summary = "A prep session of planId, by id. Its entries stay in the plan, in no session.",
            Area = McpScopes.PlanningWrite,
            Requests = [typeof(RemovePrepSessionCommand)],
            Replaces = ["remove_prep_session"],
            Build = (args, _) => new RemovePrepSessionCommand(args.RequiredGuid("planId"), args.RequiredGuid("id")),
            Shape = _ => new { removed = true },
        },
        new ToolMode
        {
            Name = "shopping_list",
            Summary = "A shopping list and all its items, by id.",
            Area = McpScopes.PlanningWrite,
            Requests = [typeof(DeleteShoppingListCommand)],
            Replaces = ["delete_shopping_list"],
            Build = (args, _) => new DeleteShoppingListCommand(args.RequiredGuid("id")),
            Shape = _ => new { deleted = true },
        },
        new ToolMode
        {
            Name = "shopping_item",
            Summary = "One item of a shopping list, by its item id.",
            Area = McpScopes.PlanningWrite,
            Requests = [typeof(DeleteShoppingListItemCommand)],
            Replaces = ["delete_shopping_list_item"],
            Build = (args, _) => new DeleteShoppingListItemCommand(args.RequiredGuid("id")),
            Shape = _ => new { deleted = true },
        },
        new ToolMode
        {
            Name = "checked_items",
            Summary = "Every ticked item of listId. Previews first: the list and the ticked items.",
            Area = McpScopes.PlanningWrite,
            Requests = [typeof(ClearCheckedShoppingListItemsCommand)],
            Replaces = ["clear_checked_items"],
            Build = (args, _) => new ClearCheckedShoppingListItemsCommand(args.RequiredGuid("listId")),
            Confirm = new ToolConfirmStep(
                typeof(GetCheckedItemsPreviewQuery),
                (args, _) => new GetCheckedItemsPreviewQuery(args.RequiredGuid("listId")),
                result => CheckedItemsFacts(result as CheckedItemsPreview)),
        },
        new ToolMode
        {
            Name = "reminder",
            Summary = "A reminder, by id. Turning it off (set_reminder update, enabled false) keeps it instead.",
            Area = McpScopes.PlanningWrite,
            Requests = [typeof(DeleteReminderCommand)],
            Replaces = ["delete_reminder"],
            Build = (args, _) => new DeleteReminderCommand(args.RequiredGuid("id")),
            Shape = _ => new { deleted = true },
        },
    ];

    private static IReadOnlyList<string> ReminderKinds => ToolReference.ReminderKinds;

    private static ToolMode StatusMode(string name, string summary, string status) => new()
    {
        Name = name,
        Summary = summary,
        Area = McpScopes.PlanningWrite,
        Requests = [typeof(SetMealPlanStatusCommand)],
        Replaces = ["set_meal_plan_status"],
        Build = (args, _) => new SetMealPlanStatusCommand(args.RequiredGuid("planId"), status),
        Shape = _ => new { status },
    };

    /// <summary>
    /// A new plan. The assistant makes an empty plan of its own: never in a household, never from a
    /// template, so a model cannot write into someone else's household (docs/AI.md).
    /// </summary>
    private static MediatR.IBaseRequest CreatePlan(ToolArgs args, ToolCallContext context)
    {
        var startDate = args.RequiredDate("startDate");
        if (context.Surface == ToolSurface.Assistant)
        {
            return new CreateMealPlanCommand
            {
                Name = args.OptionalString("name"),
                StartDate = startDate,
                DurationDays = args.OptionalInt("durationDays") ?? 7,
                HouseholdId = null,
            };
        }

        var householdId = args.OptionalGuid("householdId");
        if (args.OptionalGuid("templateId") is { } templateId)
            return new ApplyTemplateCommand(templateId, startDate, householdId, args.OptionalString("name"));

        return new CreateMealPlanCommand
        {
            Name = args.OptionalString("name"),
            Description = args.OptionalString("description"),
            StartDate = startDate,
            DurationDays = args.OptionalInt("durationDays") ?? 7,
            HouseholdId = householdId,
            Rules = args.As<PlanRulesDto>("rules"),
            Entries = args.As<List<PlanEntryRequest>>("entries") ?? [],
            PrepSessions = args.As<List<PrepSessionRequest>>("prepSessions") ?? [],
        };
    }

    /// <summary>One change of edit_meal_plan. An entry's kind follows from what is given, as on the assistant.</summary>
    private static MealPlanChange Change(ToolArgs change, int index)
    {
        var action = change.OptionalString("action")
            ?? throw new DomainValidationException($"changes[{index}] needs an action: {string.Join(", ", EditMealPlanCommand.Actions)}.");
        var entry = action is EditMealPlanCommand.AddEntry or EditMealPlanCommand.UpdateEntry;
        return new MealPlanChange(
            action,
            change.OptionalGuid("entryId"),
            change.OptionalGuid("sessionId"),
            entry ? AiToolArgs.Planning.Entry(change.Root) with { PrepSessionId = change.OptionalGuid("prepSessionId") } : null,
            entry ? null : new PrepSessionRequest
            {
                DayIndex = change.OptionalInt("dayIndex") ?? 0,
                Title = change.OptionalString("title") ?? string.Empty,
                Notes = change.OptionalString("notes"),
            });
    }

    /// <summary>The flat schedule fields read by the create reader, which needs a kind it does not use for the schedule.</summary>
    private static System.Text.Json.JsonElement WithKind(ToolArgs args)
    {
        var node = JsonNode.Parse(args.Root.GetRawText())!.AsObject();
        node["kind"] ??= "custom";
        return System.Text.Json.JsonSerializer.SerializeToElement(node);
    }

    private static string ReceiptText(PlanningBatchReceipt receipt, string what)
    {
        var done = $"Made {receipt.Completed.Count} {what}{(receipt.Completed.Count == 1 ? "" : "s")}"
            + (receipt.Completed.Any(c => c.Id is not null) ? $" ({string.Join(", ", receipt.Completed.Select(c => $"{c.Index}: {c.Id}"))})." : ".");
        if (receipt.Failed is not { } failed) return done;
        return $"{done} {char.ToUpperInvariant(what[0])}{what[1..]} {failed.Index} ({failed.Action}) failed: {failed.Error} "
            + (receipt.Remaining.Count > 0 ? $"Still to send: {string.Join(", ", receipt.Remaining)}. " : string.Empty)
            + receipt.Guidance;
    }

    /// <summary>How many days of a plan one reply carries: four days keep a busy plan well under a host's reply limit.</summary>
    public const int PlanWindowDays = 4;

    /// <summary>The days from <paramref name="fromDay"/>, four of them, with the violations that fall in them or in no day.</summary>
    private static MealPlanDetailDto PlanWindow(MealPlanDetailDto plan, int fromDay)
    {
        var first = Math.Clamp(fromDay, 0, Math.Max(0, plan.DurationDays - 1));
        var last = first + PlanWindowDays - 1;
        return plan with
        {
            Days = plan.Days.Where(d => d.DayIndex >= first && d.DayIndex <= last).ToList(),
            Violations = plan.Violations.Where(v => v.DayIndex is null || (v.DayIndex >= first && v.DayIndex <= last)).ToList(),
        };
    }

    private static string DescribePlan(MealPlanDetailDto plan)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture,
            $"\"{plan.Name}\" (plan {plan.Id}, {plan.Status}): from {plan.StartDate:yyyy-MM-dd}, {plan.DurationDays} days, {plan.EntryCount} entries.\n");
        if (plan.Days.Count > 0 && (plan.Days[0].DayIndex > 0 || plan.Days[^1].DayIndex < plan.DurationDays - 1))
        {
            var (first, last) = (plan.Days[0].DayIndex, plan.Days[^1].DayIndex);
            text.Append(CultureInfo.InvariantCulture, $"Days {first} to {last} of 0 to {plan.DurationDays - 1}");
            text.Append(last < plan.DurationDays - 1
                ? string.Create(CultureInfo.InvariantCulture, $"; ask again with fromDay {last + 1} for the next.\n")
                : ".\n");
        }
        foreach (var day in plan.Days)
        {
            var entries = day.Slots.SelectMany(s => s.Entries).ToList();
            if (entries.Count == 0) continue;
            text.Append(CultureInfo.InvariantCulture, $"Day {day.DayIndex}: ")
                .Append(string.Join("; ", entries.Select(AiPlanningTools.Entry)))
                .Append('\n');
        }
        foreach (var violation in plan.Violations) text.Append("Violation: ").Append(violation.Message).Append('\n');
        return text.ToString().TrimEnd();
    }

    /// <summary>What deleting the plan removes. Counts and names only, so the same state gives the same facts.</summary>
    private static JsonNode MealPlanPreviewFacts(MealPlanDeletePreview? preview)
    {
        if (preview is null) return new JsonObject();
        var facts = new JsonObject
        {
            ["mealPlan"] = preview.Name,
            ["id"] = preview.Id.ToString(),
            ["status"] = preview.Status,
            ["entries"] = preview.Entries,
            ["prepSessions"] = preview.PrepSessions,
            ["shoppingListsMadeFromIt"] = new JsonArray(preview.LinkedShoppingLists.Select(n => (JsonNode)JsonValue.Create(n)!).ToArray()),
        };
        if (preview.LinkedShoppingLists.Count > 0)
            facts["note"] = "The shopping lists made from it are kept, no longer linked to a plan.";
        return facts;
    }

    private static JsonNode CheckedItemsFacts(CheckedItemsPreview? preview)
    {
        if (preview is null) return new JsonObject();
        return new JsonObject
        {
            ["shoppingList"] = preview.Name,
            ["id"] = preview.ListId.ToString(),
            ["tickedItemsRemoved"] = new JsonArray(preview.CheckedItems.Select(n => (JsonNode)JsonValue.Create(n)!).ToArray()),
        };
    }
}
