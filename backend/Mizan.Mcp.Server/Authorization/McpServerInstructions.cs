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
}
