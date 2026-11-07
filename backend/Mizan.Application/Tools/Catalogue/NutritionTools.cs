using System.Text.Json.Nodes;
using Mizan.Application.Ai;
using Mizan.Application.Ai.Tools;
using Mizan.Application.Commands;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Application.Prices;
using Mizan.Application.Queries;
using Mizan.Contracts.Mcp;
using Mizan.Contracts.Meals;
using Mizan.Contracts.Nutrition;
using Mizan.Domain.Ai;
using Mizan.Domain.Constants;
using Mizan.Domain.Goals;
using Mizan.Domain.Nutrition;

namespace Mizan.Application.Tools.Catalogue;

/// <summary>Food and nutrition: foods and recipes, the diary, estimates, goals and check-ins.</summary>
public static class NutritionTools
{
    private static readonly string[] MealSlots = MealTypes.All;

    public static IReadOnlyList<TaskTool> All { get; } =
    [
        new TaskTool
        {
            Name = "find_food",
            Title = "Find foods and recipes",
            Description = "Find foods in the catalogue or the person's saved recipes, with nutrition (foods per 100 g, recipes per serving) and, for a food, its price. "
                + "Search before logging, and use the returned id: servings are a count of the food's own serving, not grams.",
            Kind = ToolKind.Read,
            ModeArgument = "kind",
            DefaultMode = "food",
            Properties = new JsonObject
            {
                ["query"] = ToolSchema.String("food, recipe: part of the name (or brand). For recipes, leave it out to list every saved recipe, favourites and the most recently logged first."),
                ["foodId"] = ToolSchema.Id("food: one food by id, with full nutrition, instead of a search."),
                ["includePrice"] = ToolSchema.Boolean("food with foodId: also the price in the person's region (per kg and as quoted, with currency and confidence)."),
                ["recipeId"] = ToolSchema.Id("recipe: one recipe by id, with ingredients, method and nutrition, instead of a search."),
                ["favoritesOnly"] = ToolSchema.Boolean("recipe: only favourites."),
                ["page"] = ToolSchema.Integer("Page, from 1."),
                ["pageSize"] = ToolSchema.Integer("Results per page, 20 by default (at most 100)."),
                ["date"] = ToolSchema.Date("fit: the day, YYYY-MM-DD; today by default."),
                ["count"] = ToolSchema.Integer("fit: how many recipes, 1 to 20 (5 by default)."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "food",
                    Summary = "Search the food catalogue by query, or read one food by foodId (with includePrice for its price). The default.",
                    Area = McpScopes.NutritionRead,
                    Surfaces = ToolSurface.Both,
                    Requests = [typeof(SearchFoodsQuery), typeof(GetFoodDetailQuery)],
                    Replaces = ["search_foods", "get_food", "get_food_price"],
                    NotFound = "There is no such food.",
                    Build = (args, context) => args.OptionalGuid("foodId") is { } foodId
                        ? new GetFoodDetailQuery(foodId, args.OptionalBool("includePrice") ?? false)
                        : new SearchFoodsQuery
                        {
                            SearchTerm = args.OptionalString("query"),
                            Page = Math.Max(1, args.OptionalInt("page") ?? 1),
                            PageSize = Math.Clamp(args.OptionalInt("pageSize") ?? (context.Surface == ToolSurface.Assistant ? 10 : 20), 1, 100),
                        },
                    Describe = result => result switch
                    {
                        PagedResult<FoodDto> { Items.Count: > 0 } foods => string.Join("\n", foods.Items.Select(FoodLine)),
                        PagedResult<FoodDto> => "No foods matched that.",
                        FoodDto food => FoodLine(food),
                        _ => TaskToolRunner.Serialize(result),
                    },
                },
                new ToolMode
                {
                    Name = "recipe",
                    Summary = "The person's saved recipes (their own, their household's and the public ones they favourited), by query or all; or one by recipeId.",
                    Area = McpScopes.NutritionRead,
                    Surfaces = ToolSurface.Both,
                    Axis = DataAxis.Planning,
                    Requests = [typeof(GetRecipesQuery), typeof(GetRecipeByIdQuery)],
                    Replaces = ["search_recipes", "get_recipe"],
                    NotFound = "There is no such recipe, or it is not the person's to read.",
                    Build = (args, _) => args.OptionalGuid("recipeId") is { } recipeId
                        ? new GetRecipeByIdQuery(recipeId)
                        : new GetRecipesQuery
                        {
                            SearchTerm = args.OptionalString("query"),
                            IncludePublic = false,
                            IncludeFavorites = true,
                            FavoritesOnly = args.OptionalBool("favoritesOnly") ?? false,
                            Page = Math.Max(1, args.OptionalInt("page") ?? 1),
                            PageSize = Math.Clamp(args.OptionalInt("pageSize") ?? 20, 1, 100),
                        },
                    Describe = result => result switch
                    {
                        PagedResult<RecipeDto> { Items.Count: > 0 } recipes => RecipesText(recipes),
                        PagedResult<RecipeDto> { TotalCount: > 0 } => "No more recipes on that page.",
                        PagedResult<RecipeDto> => "None of their saved recipes matched that.",
                        null => "There is no such recipe the person can see.",
                        _ => TaskToolRunner.Serialize(result),
                    },
                },
                new ToolMode
                {
                    Name = "fit",
                    Summary = "The saved recipes ranked, best first, by how well one serving fits what is left of the day's targets, with what each would leave. Report the ranking as given.",
                    Area = McpScopes.NutritionRead,
                    Surfaces = ToolSurface.Both,
                    Requests = [typeof(RankRecipesForDayQuery)],
                    Replaces = ["rank_recipes_for_today"],
                    Build = (args, _) => new RankRecipesForDayQuery
                    {
                        Date = args.OptionalDate("date"),
                        Count = Math.Clamp(args.OptionalInt("count") ?? 5, 1, 20),
                    },
                    Describe = result => result is RecipeFitResult fit ? AiToolCatalogue.RecipeFitText(fit) : "No ranking.",
                },
            ],
        },
        new TaskTool
        {
            Name = "get_nutrition",
            Title = "Nutrition for a day, a range, or the goal",
            Description = "What the person ate against their targets: one day's totals (with its diary entries and the goal on request), daily totals over a range, "
                + "or the goal with its history and check-ins. The day view renders the nutrition-day view in hosts that show apps.",
            Kind = ToolKind.Read,
            ModeArgument = "view",
            DefaultMode = "day",
            App = "ui://mizan/nutrition-day.html",
            Properties = new JsonObject
            {
                ["date"] = ToolSchema.Date("day: YYYY-MM-DD, today by default."),
                ["include"] = ToolSchema.Array("day: also return \"entries\" (the diary rows, by meal and logged recipe) and/or \"goal\" (the active goal).",
                    ToolSchema.Enum("A part to add.", ["entries", "goal"])),
                ["from"] = ToolSchema.Date("range, goals: the first day, YYYY-MM-DD. range: 7 days back by default; goals: 30."),
                ["to"] = ToolSchema.Date("range: the last day, YYYY-MM-DD, today by default."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "day",
                    Summary = "One day's totals against the targets, by meal; include entries and/or goal for more. The default.",
                    Area = McpScopes.NutritionRead,
                    Surfaces = ToolSurface.Both,
                    Requests = [typeof(GetNutritionDayQuery)],
                    Replaces = ["get_nutrition_summary", "get_daily_nutrition", "get_food_diary", "get_current_goal"],
                    Build = (args, _) =>
                    {
                        var include = args.StringList("include").Select(i => i.Trim().ToLowerInvariant()).ToHashSet();
                        var unknown = include.Except(["entries", "goal"]).FirstOrDefault();
                        if (unknown is not null) throw new DomainValidationException($"'include' takes entries and goal, not '{unknown}'.");
                        return new GetNutritionDayQuery(Today(args.OptionalDate("date")), include.Contains("entries"), include.Contains("goal"));
                    },
                    Describe = result => result is DailyNutritionResult day
                        ? $"{day.Date:yyyy-MM-dd}: " + NutritionText.Summary(NutritionText.Daily(
                            day.TotalCalories, day.TotalProtein, day.TotalCarbs, day.TotalFat,
                            day.TargetCalories, day.TargetProtein, day.TargetCarbs, day.TargetFat))
                        : "No totals for that day.",
                },
                new ToolMode
                {
                    Name = "range",
                    Summary = "Daily totals from from to to, at most 90 days, for trends.",
                    Area = McpScopes.NutritionRead,
                    Requests = [typeof(GetDailyNutritionRangeQuery)],
                    Replaces = ["get_nutrition_range"],
                    Build = (args, _) =>
                    {
                        var to = Today(args.OptionalDate("to"));
                        var from = args.OptionalDate("from") ?? to.AddDays(-6);
                        if (from > to) throw new DomainValidationException("'from' must be on or before 'to'.");
                        return new GetDailyNutritionRangeQuery
                        {
                            Days = Math.Clamp(to.DayNumber - from.DayNumber + 1, 1, 90),
                            EndDate = to,
                        };
                    },
                },
                new ToolMode
                {
                    Name = "goals",
                    Summary = "The active goal, every goal set before it, and the check-ins since from.",
                    Area = McpScopes.NutritionRead,
                    Requests = [typeof(GetGoalOverviewQuery)],
                    Replaces = ["get_goal_history", "get_goal_progress"],
                    Build = (args, _) =>
                    {
                        var today = Today(null);
                        var from = args.OptionalDate("from") ?? today.AddDays(-29);
                        return new GetGoalOverviewQuery(Math.Max(1, today.DayNumber - from.DayNumber + 1));
                    },
                },
            ],
        },
        new TaskTool
        {
            Name = "log_food",
            Title = "Log food eaten",
            Description = "Log one or many things eaten to the diary, and get a receipt. Each item is one of a food (foodId), a recipe (recipeId), "
                + "a meal plan entry (planEntryId with planId) or a name with the nutrition eaten. Search first; servings count the food's own serving, not grams; "
                + "never guess nutrition. The items are saved one by one: when one fails, retry only the remaining ones, and check an unconfirmed one in the diary first. "
                + "Workouts and weight have their own tools.",
            Kind = ToolKind.Write,
            Required = ["items"],
            Properties = new JsonObject
            {
                ["date"] = ToolSchema.Date("The diary day, YYYY-MM-DD; today by default. A plan entry keeps its own day unless this or the item says otherwise."),
                ["mealType"] = ToolSchema.Enum("The meal for items that do not say; SNACK by default (a plan entry keeps its slot).", MealSlots),
                ["items"] = ToolSchema.Array($"What was eaten, 1 to {LogFoodItemsCommand.MaxItems} items.", ToolSchema.Object(
                    "One thing eaten: exactly one of foodId, recipeId, planEntryId (with planId) or name.",
                    new JsonObject
                    {
                        ["foodId"] = ToolSchema.Id("A food from find_food."),
                        ["recipeId"] = ToolSchema.Id("A recipe from find_food; its own nutrition is used."),
                        ["planEntryId"] = ToolSchema.Id("A meal plan entry, logged with the nutrition the plan computed. Logging it again logs it twice."),
                        ["planId"] = ToolSchema.Id("The plan of planEntryId."),
                        ["name"] = ToolSchema.String("Something with no catalogue item: give calories (and macros), or amountGrams with caloriesPer100g."),
                        ["servings"] = ToolSchema.Number("Count of the food's serving or the recipe's servings, 1 by default."),
                        ["mealType"] = ToolSchema.Enum("This item's meal.", MealSlots),
                        ["date"] = ToolSchema.Date("This item's day, YYYY-MM-DD."),
                        ["loggedAt"] = ToolSchema.String("When it was eaten, ISO 8601; now by default."),
                        ["amountGrams"] = ToolSchema.Number("name: the weight eaten, in grams; the server scales the per-100 g figures to it."),
                        ["calories"] = ToolSchema.Number("name: total calories eaten."),
                        ["proteinGrams"] = ToolSchema.Number("name: protein eaten, g."),
                        ["carbsGrams"] = ToolSchema.Number("name: carbs eaten, g."),
                        ["fatGrams"] = ToolSchema.Number("name: fat eaten, g."),
                        ["fiberGrams"] = ToolSchema.Number("name: fibre eaten, g."),
                        ["caloriesPer100g"] = ToolSchema.Number("name: calories per 100 g, as an estimate returns them."),
                        ["proteinPer100g"] = ToolSchema.Number("name: protein per 100 g."),
                        ["carbsPer100g"] = ToolSchema.Number("name: carbs per 100 g."),
                        ["fatPer100g"] = ToolSchema.Number("name: fat per 100 g."),
                        ["fiberPer100g"] = ToolSchema.Number("name: fibre per 100 g."),
                    })),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "items",
                    Summary = "Log the items; returns what was saved, what failed and what remains.",
                    Area = McpScopes.NutritionWrite,
                    Surfaces = ToolSurface.Both,
                    Requests = [typeof(LogFoodItemsCommand)],
                    Replaces = ["log_food", "log_meal", "log_meal_manual", "log_day", "log_plan_entry"],
                    Build = (args, context) => new LogFoodItemsCommand(
                        args.OptionalDate("date"),
                        args.OptionalString("mealType"),
                        args.As<List<LogFoodItem>>("items") ?? throw new DomainValidationException("'items' is required: a list of what was eaten."),
                        // The assistant does not log plan entries; an outside agent needs the plans it reads them from.
                        AllowPlanEntries: context.Surface == ToolSurface.Mcp
                            && (context.Grant is null || McpScopes.Allows(context.Grant.Scopes, McpScopes.PlanningRead))),
                    Describe = result => result is LogFoodReceipt receipt ? ReceiptText(receipt) : "Logged that.",
                },
            ],
        },
        new TaskTool
        {
            Name = "estimate_food",
            Title = "Estimate a meal",
            Description = "Estimate the foods and macros of a meal from a photo or from words. Pro only, and it spends the AI allowance. Nothing is logged: "
                + "show the estimate, then log it with log_food. Each food says its source: \"recipe\" (recipeId, servings), \"catalogue\" (foodId) or \"estimate\" (log by name with its figures). "
                + "A photo renders the food-photo view in hosts that show apps.",
            Kind = ToolKind.Write,
            ModeArgument = "kind",
            DefaultMode = "text",
            App = "ui://mizan/food-photo.html",
            Properties = new JsonObject
            {
                ["text"] = ToolSchema.String($"text: what was eaten, in words, up to {EstimateFoodFromTextCommand.MaxDescriptionLength} characters."),
                ["imageUrl"] = ToolSchema.String("photo: a public https address of the original photo (JPEG, PNG or WebP, up to 4 MB). Preferred: Mizan downloads it."),
                ["imageBase64"] = ToolSchema.String("photo: the original file's bytes in base64, only when there is no address. Never shrink, crop or re-encode the photo."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "text",
                    Summary = "From a description in words. The default.",
                    Area = McpScopes.NutritionWrite,
                    Requests = [typeof(EstimateFoodFromTextCommand)],
                    Replaces = ["analyze_food_text"],
                    Build = (args, _) => new EstimateFoodFromTextCommand(args.OptionalString("text") ?? args.OptionalString("description")),
                },
                new ToolMode
                {
                    Name = "photo",
                    Summary = "From a photo: imageUrl (preferred) or imageBase64. The photo is kept 30 days; delete kind food_photo removes it sooner.",
                    Area = McpScopes.NutritionWrite,
                    Requests = [typeof(EstimateFoodFromPhotoCommand)],
                    Replaces = ["analyze_food_image"],
                    Build = (args, _) => new EstimateFoodFromPhotoCommand(args.OptionalBase64("imageBase64"), args.OptionalString("imageUrl")),
                },
            ],
        },
        new TaskTool
        {
            Name = "save_food",
            Title = "Save a food, recipe or price",
            Description = "Create or change the person's own foods and recipes: a custom food, a recipe saved from a logged meal or edited, a favourite, "
                + "an ingredient made from a recipe, a recipe photo, or a report that a food's price is wrong. Never guess nutrition or weights; ask the person.",
            Kind = ToolKind.Write,
            ModeArgument = "kind",
            Properties = new JsonObject
            {
                ["foodId"] = ToolSchema.Id("food: the food to change (leave out to create one). price_report: the food."),
                ["name"] = ToolSchema.String("food: the name."),
                ["brand"] = ToolSchema.String("food: the brand."),
                ["barcode"] = ToolSchema.String("food: EAN or UPC."),
                ["caloriesPer100g"] = ToolSchema.Number("food: calories per 100 g."),
                ["proteinPer100g"] = ToolSchema.Number("food: protein per 100 g."),
                ["carbsPer100g"] = ToolSchema.Number("food: carbs per 100 g."),
                ["fatPer100g"] = ToolSchema.Number("food: fat per 100 g."),
                ["fiberPer100g"] = ToolSchema.Number("food: fibre per 100 g."),
                ["sugarPer100g"] = ToolSchema.Number("food: sugar per 100 g."),
                ["sodiumPer100g"] = ToolSchema.Number("food: sodium per 100 g, mg."),
                ["servingSize"] = ToolSchema.Number("food: one serving, in servingUnit (100 by default)."),
                ["servingUnit"] = ToolSchema.String("food: g, ml or oz (g by default)."),
                ["recipeId"] = ToolSchema.Id("recipe: the recipe to change or favourite. ingredient: the recipe to make an ingredient from."),
                ["fromLog"] = ToolSchema.Object(
                    "recipe: save a logged meal (at least two items) as a new private recipe; the ingredients come from the diary. "
                    + "If the reply asks for missing weights, ask the person and retry with recipeYields or entryWeightsGrams.",
                    new JsonObject
                    {
                        ["date"] = ToolSchema.Date("The day the meal was logged."),
                        ["mealType"] = ToolSchema.Enum("The logged meal.", MealSlots),
                        ["recipeYields"] = ToolSchema.Object("Recipe id to the finished weight of that whole recipe, g; only when asked."),
                        ["entryWeightsGrams"] = ToolSchema.Object("Diary entry id to the weight eaten, g; only when asked."),
                    },
                    "date", "mealType"),
                ["title"] = ToolSchema.String("recipe: the recipe's name."),
                ["description"] = ToolSchema.String("recipe: a description."),
                ["servings"] = ToolSchema.Integer("recipe: servings the recipe makes."),
                ["prepTimeMinutes"] = ToolSchema.Integer("recipe: preparation time."),
                ["cookTimeMinutes"] = ToolSchema.Integer("recipe: cooking time."),
                ["instructions"] = ToolSchema.String("recipe: the method; newlines separate steps."),
                ["isPublic"] = ToolSchema.Boolean("recipe: share it publicly."),
                ["imageUrl"] = ToolSchema.String("recipe: the photo's address (from kind image). image: a public https address of the image to store."),
                ["ingredients"] = ToolSchema.Array("recipe, when changing it: every ingredient (the list is replaced).", ToolSchema.Object(
                    "One ingredient.",
                    new JsonObject
                    {
                        ["ingredientText"] = ToolSchema.String("As a person reads it, e.g. \"200 g chicken breast\"."),
                        ["foodId"] = ToolSchema.Id("The catalogue food, when known."),
                        ["amount"] = ToolSchema.Number("Grams."),
                        ["unit"] = ToolSchema.String("The unit of amount."),
                    },
                    "ingredientText")),
                ["householdId"] = ToolSchema.Id("recipe with fromLog: a household to share the new recipe with."),
                ["favorite"] = ToolSchema.Boolean("recipe with recipeId and no title: true to make it a favourite, false to remove it."),
                ["yieldGrams"] = ToolSchema.Number("ingredient: the finished weight of the whole recipe, g. Never guess it."),
                ["suggestedPrice"] = ToolSchema.Number("price_report: what the person paid, in the region's currency."),
                ["quantity"] = ToolSchema.Number("price_report: how much they bought for that price."),
                ["unit"] = ToolSchema.Enum("price_report: the unit of quantity.", ["g", "kg", "ml", "l", "piece"]),
                ["note"] = ToolSchema.String("price_report: where and when, optional."),
                ["imageBase64"] = ToolSchema.String("image: the original file's bytes in base64, only when there is no imageUrl."),
                ["fileName"] = ToolSchema.String("image: the file's name, with imageBase64."),
                ["folder"] = ToolSchema.Enum("image: recipes (default) or avatars.", ["recipes", "avatars"]),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "food",
                    Summary = "Create a custom food with its nutrition per 100 g, or change one with foodId.",
                    Area = McpScopes.NutritionWrite,
                    Requests = [typeof(CreateFoodCommand), typeof(UpdateFoodCommand)],
                    Replaces = ["create_food", "update_food"],
                    Build = (args, _) => args.OptionalGuid("foodId") is { } foodId
                        ? args.Body<UpdateFoodCommand>() with { Id = foodId }
                        : args.Body<CreateFoodCommand>(),
                },
                new ToolMode
                {
                    Name = "recipe",
                    Summary = "With fromLog, save a logged meal as a recipe; with recipeId and title, change a recipe (ingredients are replaced); with recipeId and favorite, set it as a favourite or not.",
                    Area = McpScopes.NutritionWrite,
                    Requests = [typeof(PromoteMealToRecipeCommand), typeof(UpdateRecipeCommand), typeof(ToggleFavoriteRecipeCommand)],
                    Replaces = ["promote_to_recipe", "update_recipe", "toggle_favorite_recipe"],
                    Build = (args, context) =>
                    {
                        if (args.Has("fromLog"))
                        {
                            var log = args.Object("fromLog");
                            return new PromoteMealToRecipeCommand(
                                log.RequiredDate("date"),
                                log.RequiredString("mealType").Trim().ToUpperInvariant(),
                                args.RequiredString("title").Trim(),
                                context.Surface == ToolSurface.Mcp ? args.OptionalGuid("householdId") : null,
                                log.As<Dictionary<Guid, decimal>>("recipeYields"),
                                log.As<Dictionary<Guid, decimal>>("entryWeightsGrams"));
                        }

                        var recipeId = args.OptionalGuid("recipeId")
                            ?? throw new DomainValidationException("Give fromLog to save a logged meal, or recipeId to change a recipe.");
                        if (args.OptionalBool("favorite") is { } favorite && !args.Has("title"))
                            return new ToggleFavoriteRecipeCommand { RecipeId = recipeId, Favorite = favorite };

                        args.RequiredString("title");
                        return args.Body<UpdateRecipeCommand>() with { Id = recipeId };
                    },
                    Shape = result => result is Guid id ? new { recipeId = id } : result,
                },
                new ToolMode
                {
                    Name = "ingredient",
                    Summary = "Make an ingredient (a food) from an owned recipe, with the batch's finished weight in yieldGrams.",
                    Area = McpScopes.NutritionWrite,
                    Requests = [typeof(PromoteRecipeToPreparationCommand)],
                    Replaces = ["promote_recipe_to_preparation"],
                    Build = (args, _) => new PromoteRecipeToPreparationCommand(args.RequiredGuid("recipeId"), args.RequiredDecimal("yieldGrams")),
                    Shape = result => result is Guid id ? new { foodId = id } : result,
                },
                new ToolMode
                {
                    Name = "price_report",
                    Summary = "Report that a food's price is wrong, with what the person paid: suggestedPrice for quantity of unit. An administrator reviews it.",
                    Area = McpScopes.NutritionWrite,
                    Requests = [typeof(CreatePriceReportCommand)],
                    Replaces = ["report_food_price"],
                    Build = (args, _) => new CreatePriceReportCommand(
                        args.RequiredGuid("foodId"),
                        args.RequiredDecimal("suggestedPrice"),
                        args.RequiredDecimal("quantity"),
                        args.RequiredString("unit"),
                        args.OptionalString("note")),
                    Shape = result => result is Guid id ? new { reportId = id } : result,
                },
                new ToolMode
                {
                    Name = "image",
                    Summary = "Store an image (imageUrl preferred, or imageBase64) and return its address, for a recipe's imageUrl.",
                    Area = McpScopes.NutritionWrite,
                    Requests = [typeof(UploadImageCommand)],
                    Replaces = ["upload_image"],
                    Build = (args, _) => new UploadImageCommand(
                        args.OptionalString("folder")?.Trim().ToLowerInvariant() switch
                        {
                            null or "" or "recipes" or "recipe" => StorageFolder.Recipes,
                            "avatars" or "avatar" => StorageFolder.Avatars,
                            var other => throw new DomainValidationException($"'folder' is recipes or avatars, not '{other}'."),
                        },
                        args.OptionalString("fileName"),
                        args.OptionalBase64("imageBase64"),
                        args.OptionalString("imageUrl")),
                },
            ],
        },
        new TaskTool
        {
            Name = "set_goal",
            Title = "Set the goal or check in",
            Description = "Set the person's nutrition goal and daily targets (this replaces the active goal; the old one stays in the history), "
                + "or record a day's check-in against it. A check-in takes that day's diary for what is left out.",
            Kind = ToolKind.Write,
            ModeArgument = "action",
            DefaultMode = "set",
            Properties = new JsonObject
            {
                ["goalType"] = ToolSchema.Enum("set: the kind of goal.", GoalTypes.All),
                ["targetCalories"] = ToolSchema.Integer("set: calories a day."),
                ["targetProteinGrams"] = ToolSchema.Number("set: protein a day, g."),
                ["targetCarbsGrams"] = ToolSchema.Number("set: carbs a day, g."),
                ["targetFatGrams"] = ToolSchema.Number("set: fat a day, g."),
                ["targetFiberGrams"] = ToolSchema.Number("set: fibre a day, g."),
                ["targetWeight"] = ToolSchema.Number("set: the target weight, in weightUnit."),
                ["weightUnit"] = ToolSchema.Enum("set: kg (default) or lb.", ["kg", "lb"]),
                ["targetDate"] = ToolSchema.Date("set: the date to reach it by."),
                ["date"] = ToolSchema.Date("check_in: the day, today by default. A goal keeps one check-in a day; the same day again replaces it."),
                ["actualCalories"] = ToolSchema.Integer("check_in: calories eaten; from the diary when left out."),
                ["actualProteinGrams"] = ToolSchema.Number("check_in: protein eaten, g."),
                ["actualCarbsGrams"] = ToolSchema.Number("check_in: carbs eaten, g."),
                ["actualFatGrams"] = ToolSchema.Number("check_in: fat eaten, g."),
                ["currentWeightKg"] = ToolSchema.Number("check_in: weight in kg; that day's body measurement when left out. Needs body write permission too."),
                ["notes"] = ToolSchema.String("check_in: a note."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "set",
                    Summary = "Set the goal and targets; every target is optional. The default.",
                    Area = McpScopes.NutritionWrite,
                    Surfaces = ToolSurface.Both,
                    Requests = [typeof(CreateUserGoalCommand)],
                    Replaces = ["create_goal"],
                    Build = (args, _) =>
                    {
                        var weight = args.OptionalDecimal("targetWeight");
                        return new CreateUserGoalCommand
                        {
                            GoalType = args.OptionalString("goalType"),
                            TargetCalories = args.OptionalInt("targetCalories"),
                            TargetProteinGrams = args.OptionalDecimal("targetProteinGrams"),
                            TargetCarbsGrams = args.OptionalDecimal("targetCarbsGrams"),
                            TargetFatGrams = args.OptionalDecimal("targetFatGrams"),
                            TargetFiberGrams = args.OptionalDecimal("targetFiberGrams"),
                            TargetWeight = weight,
                            WeightUnit = weight.HasValue ? args.OptionalString("weightUnit") ?? "kg" : args.OptionalString("weightUnit"),
                            TargetDate = args.OptionalDate("targetDate"),
                        };
                    },
                    Describe = result => result is CreateUserGoalResult goal && !string.IsNullOrWhiteSpace(goal.Message)
                        ? goal.Message
                        : "Saved your targets.",
                },
                new ToolMode
                {
                    Name = "check_in",
                    Summary = "Record a day's check-in against the active goal; returns what was stored.",
                    Area = McpScopes.NutritionWrite,
                    Surfaces = ToolSurface.Both,
                    Requests = [typeof(RecordGoalProgressCommand)],
                    Replaces = ["record_goal_progress"],
                    Build = (args, context) => new RecordGoalProgressCommand
                    {
                        ActualCalories = args.OptionalInt("actualCalories"),
                        ActualProteinGrams = args.OptionalDecimal("actualProteinGrams"),
                        ActualCarbsGrams = args.OptionalDecimal("actualCarbsGrams"),
                        ActualFatGrams = args.OptionalDecimal("actualFatGrams"),
                        // The assistant's nutrition consent does not cover body data, so it never records a weight here.
                        ActualWeight = context.Surface == ToolSurface.Assistant ? null : args.OptionalDecimal("currentWeightKg"),
                        Date = args.OptionalDate("date"),
                        Notes = args.OptionalString("notes"),
                    },
                    Describe = _ => "Recorded your progress for that day.",
                },
            ],
        },
    ];

    public static IReadOnlyList<ToolMode> DeleteKinds { get; } =
    [
        new ToolMode
        {
            Name = "food",
            Summary = "A custom food, by id.",
            Area = McpScopes.NutritionWrite,
            Requests = [typeof(DeleteFoodCommand)],
            Replaces = ["delete_food"],
            Build = (args, _) => new DeleteFoodCommand { Id = args.RequiredGuid("id") },
        },
        new ToolMode
        {
            Name = "diary_entry",
            Summary = "One diary row, by id.",
            Area = McpScopes.NutritionWrite,
            Requests = [typeof(DeleteFoodDiaryEntryCommand)],
            Replaces = ["delete_meal"],
            Build = (args, _) => new DeleteFoodDiaryEntryCommand { Id = args.RequiredGuid("id") },
        },
        new ToolMode
        {
            Name = "diary_group",
            Summary = "A whole logged recipe: every diary row with that groupId (id).",
            Area = McpScopes.NutritionWrite,
            Requests = [typeof(DeleteDiaryGroupCommand)],
            Replaces = ["delete_meal_group"],
            Build = (args, _) => new DeleteDiaryGroupCommand(args.RequiredGuid("id")),
        },
        new ToolMode
        {
            Name = "goal",
            Summary = "A goal and its check-ins, by id. Deleting the active goal leaves none active.",
            Area = McpScopes.NutritionWrite,
            Requests = [typeof(DeleteUserGoalCommand)],
            Replaces = ["delete_goal"],
            Build = (args, _) => new DeleteUserGoalCommand(args.RequiredGuid("id")),
            Shape = result => new { deleted = result is true },
        },
        new ToolMode
        {
            Name = "goal_check_in",
            Summary = "One goal check-in, by id.",
            Area = McpScopes.NutritionWrite,
            Requests = [typeof(DeleteGoalProgressCommand)],
            Replaces = ["delete_goal_progress"],
            Build = (args, _) => new DeleteGoalProgressCommand(args.RequiredGuid("id")),
            Shape = result => new { deleted = result is true },
        },
        new ToolMode
        {
            Name = "recipe",
            Summary = "An owned recipe, by id. Previews first: its name and what uses it.",
            Area = McpScopes.NutritionWrite,
            Requests = [typeof(DeleteRecipeCommand)],
            Replaces = ["delete_recipe"],
            Build = (args, _) => new DeleteRecipeCommand { Id = args.RequiredGuid("id") },
            Confirm = new ToolConfirmStep(
                typeof(GetRecipeDeletePreviewQuery),
                (args, _) => new GetRecipeDeletePreviewQuery(args.RequiredGuid("id")),
                result => RecipePreviewFacts(result as RecipeDeletePreview)),
        },
        new ToolMode
        {
            Name = "food_photo",
            Summary = "A photo sent for an estimate, by the imageUrl the estimate returned (removed 30 days on anyway).",
            Area = McpScopes.NutritionWrite,
            Requests = [typeof(DeleteAnalysisPhotoCommand)],
            Replaces = ["delete_food_photo"],
            Build = (args, _) => new DeleteAnalysisPhotoCommand(args.RequiredString("imageUrl")),
        },
    ];

    private static DateOnly Today(DateOnly? date) => date ?? DateOnly.FromDateTime(DateTime.UtcNow);

    private static string FoodLine(FoodDto f) =>
        $"{f.Id} — {f.Name} (per 100g) - {f.CaloriesPer100g} kcal, P{f.ProteinPer100g} C{f.CarbsPer100g} F{f.FatPer100g}";

    private static string RecipesText(PagedResult<RecipeDto> recipes) =>
        string.Join("\n", recipes.Items.Select(r =>
            $"{r.Id} — {r.Title} ({r.Servings} servings{(r.IsFavorited ? ", favourite" : "")})"
            + (r.Nutrition is { CaloriesPerServing: { } kcal } n
                ? $" - per serving {kcal:0} kcal, P{n.ProteinGrams:0.#} C{n.CarbsGrams:0.#} F{n.FatGrams:0.#}"
                : " - no nutrition yet")))
        + (recipes.Page < recipes.TotalPages
            ? $"\nPage {recipes.Page} of {recipes.TotalPages} ({recipes.TotalCount} recipes); ask for page {recipes.Page + 1} for more."
            : string.Empty);

    private static string ReceiptText(LogFoodReceipt receipt)
    {
        var done = receipt.Completed.Count == 1 ? "Logged 1 item." : $"Logged {receipt.Completed.Count} items.";
        if (receipt.Failed is not { } failed) return done;
        return $"{done} Item {failed.Index} ({failed.Kind}) failed: {failed.Error} "
            + (receipt.Remaining.Count > 0 ? $"Still to log: items {string.Join(", ", receipt.Remaining)}. " : string.Empty)
            + receipt.Guidance;
    }

    /// <summary>What deleting the recipe touches. Counts and names only, so the same state gives the same facts.</summary>
    private static JsonNode RecipePreviewFacts(RecipeDeletePreview? preview)
    {
        if (preview is null) return new JsonObject();
        var facts = new JsonObject
        {
            ["recipe"] = preview.Title,
            ["id"] = preview.Id.ToString(),
            ["ingredients"] = preview.Ingredients,
            ["mealPlansUsingIt"] = preview.MealPlans,
            ["yourDiaryEntriesThatLoggedIt"] = preview.DiaryEntries,
            ["ingredientMadeFromIt"] = preview.PreparationFood,
        };
        if (preview.MealPlans > 0)
            facts["note"] = $"It is used in {preview.MealPlans} meal plan(s), so it cannot be deleted until it is removed from them.";
        return facts;
    }
}
