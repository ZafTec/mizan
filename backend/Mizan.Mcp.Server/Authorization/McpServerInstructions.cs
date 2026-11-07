namespace Mizan.Mcp.Server.Authorization;

/// <summary>
/// Sent to every client when it connects. These are the rules agents get wrong
/// most often, so they travel with the server instead of living only in docs.
/// </summary>
public static class McpServerInstructions
{
    public const string Text = """
        Mizan records meals, workouts and body measurements for one person.

        Logging food:
        - Search before you log (search_foods), then log with log_food using the returned id.
        - "servings" is a count of the food's own serving size, not grams. Read the search result and convert the amount eaten before you send it.
        - log_meal takes a recipe id and recipe servings. Use log_meal_manual only for known nutrition with no catalogue item.
        - Never guess nutrition numbers or a recipe's batch weight. Ask the person.

        Several entries at once:
        - log_day is not one database transaction. It returns a receipt of what completed, failed and remains. Retry only what remains, and check an unconfirmed write before you repeat it.

        Recipes and plans:
        - Recipes are made from logged meals (promote_to_recipe, at least two items). If the API asks for missing weights, ask the person.
        - Meal plans and shopping lists can belong to a household. This connection sees only the households the person allowed.

        This connection holds only the permissions the person gave it. Tools outside them are not listed. If a call is refused, say what permission is missing and where the person can change it: Mizan, More, Connected apps.
        """;

    /// <summary>The same rules for the task tools (<c>Mcp:Toolset</c> tasks).</summary>
    public const string TasksText = """
        Mizan records meals, workouts and body measurements for one person. Each tool covers one task; a mode argument (view, kind, action or section) picks what it does. Each argument says which modes read it: send only those the mode reads, and leave out the rest rather than filling them with empty values.

        Logging food:
        - Find before you log: find_food (kind food for the catalogue, kind recipe for saved recipes), then log_food with items, each one a foodId, a recipeId, a planEntryId with its planId, or a name with the nutrition the person gave.
        - "servings" is a count of the food's own serving (find_food shows its size), not grams. When the person gave grams, send amountGrams instead and Mizan converts.
        - Never guess nutrition numbers or a recipe's batch weight. Ask the person. estimate_food gives an estimate to confirm; it logs nothing.

        Several things at once:
        - Send them in one call: log_food takes many items, edit_meal_plan many changes, update_shopping_list add_items many items. These are not one database transaction. The receipt says what completed, what failed and what remains. Retry only what remains, and check an unconfirmed write before you repeat it.

        Removing things:
        - delete removes one thing by kind and id. A high-impact kind (a meal plan, a recipe, a household membership, the social profile, ticked shopping items) first returns a preview and a token and removes nothing. Show the person the preview; only when they agree, call delete with kind "confirm" and that token. It works once.

        Reference:
        - The fixed values the tools take (price regions, goal types, meal slots, macro modes, reminder kinds) are in the resource mizan://reference and its parts such as mizan://reference/price-regions.

        Plans and households:
        - Meal plans and shopping lists can belong to a household. This connection sees only the households the person allowed. Nutrition and costs come from Mizan; quote them, do not add them up.

        This connection holds only the permissions the person gave it. Tools and modes outside them are not listed. If a call is refused, say what permission is missing and where the person can change it: Mizan, More, Connected apps.
        """;

    public static string For(McpToolset toolset) => toolset == McpToolset.Tasks ? TasksText : Text;
}
