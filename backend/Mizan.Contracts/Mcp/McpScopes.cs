namespace Mizan.Contracts.Mcp;

/// <summary>One thing a user can allow a connected app to do, as the consent screen words it.</summary>
public sealed record McpScopeInfo(
    string Group,
    string Title,
    string Description,
    bool HasWrite,
    bool AdminOnly = false);

/// <summary>
/// The scopes a connected app can hold. Each group has a read scope and,
/// where it makes sense, a write scope. Write includes read. The MCP server
/// maps every tool to one of these and refuses a call the grant does not cover.
/// </summary>
public static class McpScopes
{
    public const string ProfileRead = "profile:read";
    public const string ProfileWrite = "profile:write";
    public const string NutritionRead = "nutrition:read";
    public const string NutritionWrite = "nutrition:write";
    public const string TrainingRead = "training:read";
    public const string TrainingWrite = "training:write";
    public const string BodyRead = "body:read";
    public const string BodyWrite = "body:write";
    public const string RecipesRead = "recipes:read";
    public const string RecipesWrite = "recipes:write";
    public const string PlanningRead = "planning:read";
    public const string PlanningWrite = "planning:write";
    public const string HouseholdsRead = "households:read";
    public const string HouseholdsWrite = "households:write";
    public const string SocialRead = "social:read";
    public const string SocialWrite = "social:write";
    public const string NotificationsRead = "notifications:read";
    public const string NotificationsWrite = "notifications:write";
    public const string TrainerRead = "trainer:read";
    public const string TrainerWrite = "trainer:write";
    public const string AiUse = "ai:use";
    public const string Admin = "admin";

    /// <summary>Granted only to first-party apps. It covers every scope.</summary>
    public const string Full = "full";

    public static readonly IReadOnlyDictionary<string, McpScopeInfo> Groups = new Dictionary<string, McpScopeInfo>
    {
        ["profile"] = new("profile", "Profile", "Your name, preferences and data export.", true),
        ["nutrition"] = new("nutrition", "Food and nutrition", "Food diary, foods, daily totals and calorie goals.", true),
        ["training"] = new("training", "Training", "Workouts, workout templates and exercises.", true),
        ["body"] = new("body", "Body measurements", "Weight and other measurements.", true),
        ["recipes"] = new("recipes", "Recipes", "Your recipes and favorites.", true),
        ["planning"] = new("planning", "Meal plans and shopping", "Meal plans and shopping lists.", true),
        ["households"] = new("households", "Households", "See households, invite and remove members, switch the active household.", true),
        ["social"] = new("social", "Social", "Your profile page, feed, follows and comments.", true),
        ["notifications"] = new("notifications", "Notifications", "Read and clear notifications.", true),
        ["trainer"] = new("trainer", "Coaching", "Trainer and client tools. Client data still follows what each client shared.", true),
        ["ai"] = new("ai", "Mizan assistant", "Ask the assistant and analyze photos. Uses your daily AI allowance.", false),
        ["admin"] = new("admin", "Administration", "Admin tools. Only administrators can grant this.", false, AdminOnly: true),
    };

    public static IReadOnlyList<string> All { get; } = BuildAll();

    private static List<string> BuildAll()
    {
        var all = new List<string>();
        foreach (var (group, info) in Groups)
        {
            if (group == "ai") { all.Add(AiUse); continue; }
            if (group == "admin") { all.Add(Admin); continue; }
            all.Add($"{group}:read");
            if (info.HasWrite) all.Add($"{group}:write");
        }
        return all;
    }

    public static bool IsKnown(string scope) => scope == Full || All.Contains(scope, StringComparer.Ordinal);

    /// <summary>
    /// Drops unknown scopes, makes write imply read, removes duplicates and
    /// keeps admin only when the user is an administrator.
    /// </summary>
    public static List<string> Normalize(IEnumerable<string>? scopes, bool allowAdmin)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in scopes ?? [])
        {
            var scope = raw.Trim();
            if (!All.Contains(scope, StringComparer.Ordinal)) continue;
            if (scope == Admin && !allowAdmin) continue;
            set.Add(scope);
            if (scope.EndsWith(":write", StringComparison.Ordinal))
            {
                set.Add(scope[..^":write".Length] + ":read");
            }
        }
        return All.Where(set.Contains).ToList();
    }

    /// <summary>Whether a grant holding <paramref name="held"/> may do something that needs <paramref name="required"/>.</summary>
    public static bool Allows(IEnumerable<string> held, string required)
    {
        var heldSet = held as ICollection<string> ?? held.ToList();
        if (heldSet.Contains(Full)) return required != Admin || heldSet.Contains(Admin);
        if (heldSet.Contains(required)) return true;
        if (required.EndsWith(":read", StringComparison.Ordinal))
        {
            return heldSet.Contains(required[..^":read".Length] + ":write");
        }
        return false;
    }

    /// <summary>Parses the space separated form used on the wire.</summary>
    public static List<string> Parse(string? scope) =>
        string.IsNullOrWhiteSpace(scope)
            ? new List<string>()
            : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
}
