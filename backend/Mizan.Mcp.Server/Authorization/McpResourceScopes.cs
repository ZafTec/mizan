using Mizan.Contracts.Mcp;

namespace Mizan.Mcp.Server.Authorization;

/// <summary>
/// The scope a resource needs, decided by what it reads. A resource can never
/// show more than the tool that reads the same thing, so each one reuses that
/// tool's scope. Skills are public instructions and need none.
/// </summary>
public static class McpResourceScopes
{
    private static readonly Dictionary<string, string> ByHost = new(StringComparer.OrdinalIgnoreCase)
    {
        ["profile"] = McpScopes.ProfileRead,
        ["streak"] = McpScopes.ProfileRead,
        ["goals"] = McpScopes.NutritionRead,
        ["diary"] = McpScopes.NutritionRead,
        ["nutrition"] = McpScopes.NutritionRead,
        ["households"] = McpScopes.HouseholdsRead,
        ["recipes"] = McpScopes.RecipesRead,
        ["workouts"] = McpScopes.TrainingRead,
        ["meal-plans"] = McpScopes.PlanningRead,
        ["shopping-lists"] = McpScopes.PlanningRead,
    };

    private static readonly Dictionary<string, string> AppScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nutrition-day.html"] = McpScopes.NutritionRead,
        ["food-photo.html"] = McpScopes.AiUse,
        ["body-trend.html"] = McpScopes.BodyRead,
    };

    /// <summary>Null means the resource is public (a skill). A resource nobody mapped is refused.</summary>
    public static bool TryGet(string uri, out string? scope)
    {
        scope = null;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme == "skill") return true;
        if (parsed.Scheme == "ui")
        {
            // An app page holds no data. It reads through tools, so it needs the scope its tool needs.
            if (!AppScopes.TryGetValue(parsed.AbsolutePath.Trim('/'), out var appScope)) return false;
            scope = appScope;
            return true;
        }
        if (parsed.Scheme != "mizan") return false;
        if (!ByHost.TryGetValue(parsed.Host, out var found)) return false;
        scope = found;
        return true;
    }

    /// <summary>The same resource family under one name, so usage groups instead of listing every id.</summary>
    public static string Pattern(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)) return "unknown";
        if (parsed.Scheme == "skill") return "skill://" + parsed.Host;
        if (parsed.Scheme == "ui") return "ui://mizan/" + parsed.AbsolutePath.Trim('/');

        var last = parsed.AbsolutePath.Trim('/');
        return parsed.Host.ToLowerInvariant() switch
        {
            "diary" => "mizan://diary/{date}",
            "nutrition" when last == "today" => "mizan://nutrition/today",
            "nutrition" => "mizan://nutrition/{date}",
            "recipes" or "workouts" or "meal-plans" or "shopping-lists" => $"mizan://{parsed.Host}/{{id}}",
            _ => last.Length == 0 ? $"mizan://{parsed.Host}" : $"mizan://{parsed.Host}/{last}",
        };
    }

    /// <summary>The scope each prompt needs: the scope of the tools it tells the model to use.</summary>
    public static readonly IReadOnlyDictionary<string, string> PromptScopes = new Dictionary<string, string>
    {
        ["log_my_day"] = McpScopes.NutritionWrite,
        ["plan_my_week"] = McpScopes.PlanningWrite,
        ["grocery_run"] = McpScopes.PlanningWrite,
        ["weekly_review"] = McpScopes.NutritionRead,
        ["workout_planner"] = McpScopes.TrainingWrite,
        ["macro_rescue"] = McpScopes.NutritionRead,
        ["recipe_from_leftovers"] = McpScopes.RecipesWrite,
        ["trainer_checkin"] = McpScopes.TrainerRead,
    };
}
