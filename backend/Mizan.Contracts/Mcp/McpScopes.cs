namespace Mizan.Contracts.Mcp;

/// <summary>One thing a user can allow a connected app to do, as the consent screen words it.</summary>
public sealed record McpScopeInfo(
    string Group,
    string Title,
    string Description,
    bool HasWrite,
    bool AdminOnly = false);

/// <summary>
/// The scopes a connected app can hold.
///
/// Seven areas, each read or write, are what a person grants: nutrition, training, body,
/// planning, social, coaching and admin (docs/MCP.md#what-a-connection-can-see-and-call). Grants
/// made before them hold the older, narrower scopes (recipes, households, notifications, profile,
/// trainer, ai:use, admin). Both stay valid. An area implies the older scopes it took over, so a
/// check written against an older scope still answers for a grant stored in areas, and
/// <see cref="Mizan.Contracts.Tools.ToolAreas"/> shows an older grant as areas. Write includes read.
/// </summary>
public static class McpScopes
{
    public const string NutritionRead = "nutrition:read";
    public const string NutritionWrite = "nutrition:write";
    public const string TrainingRead = "training:read";
    public const string TrainingWrite = "training:write";
    public const string BodyRead = "body:read";
    public const string BodyWrite = "body:write";
    public const string PlanningRead = "planning:read";
    public const string PlanningWrite = "planning:write";
    public const string SocialRead = "social:read";
    public const string SocialWrite = "social:write";
    public const string CoachingRead = "coaching:read";
    public const string CoachingWrite = "coaching:write";
    public const string AdminRead = "admin:read";
    public const string AdminWrite = "admin:write";

    // The scopes of grants made before the seven areas. Still accepted and still checked.
    public const string ProfileRead = "profile:read";
    public const string ProfileWrite = "profile:write";
    public const string RecipesRead = "recipes:read";
    public const string RecipesWrite = "recipes:write";
    public const string HouseholdsRead = "households:read";
    public const string HouseholdsWrite = "households:write";
    public const string NotificationsRead = "notifications:read";
    public const string NotificationsWrite = "notifications:write";
    public const string TrainerRead = "trainer:read";
    public const string TrainerWrite = "trainer:write";
    public const string AiUse = "ai:use";
    public const string Admin = "admin";

    /// <summary>Granted only to first-party apps. It covers every scope but administration.</summary>
    public const string Full = "full";

    /// <summary>The seven areas a person grants, in the order the screens show them.</summary>
    public static readonly IReadOnlyDictionary<string, McpScopeInfo> Groups = new Dictionary<string, McpScopeInfo>
    {
        ["nutrition"] = new("nutrition", "Food and nutrition", "Food diary, foods, recipes, prices, goals and check-ins. Photo and text estimates use your AI allowance.", true),
        ["training"] = new("training", "Training", "Workouts, drafts, workout templates and exercises.", true),
        ["body"] = new("body", "Body measurements", "Weight and other measurements.", true),
        ["planning"] = new("planning", "Plans, lists and households", "Meal plans, shopping lists, reminders and your households.", true),
        ["social"] = new("social", "Social and notifications", "Your profile page, feed, follows, comments and notifications.", true),
        ["coaching"] = new("coaching", "Coaching", "Your trainer, requests and coach chat; for coaches, their clients. Client data still follows what each client shared.", true),
        ["admin"] = new("admin", "Administration", "Admin tools. Only administrators can grant this.", true, AdminOnly: true),
    };

    /// <summary>The fourteen area scopes.</summary>
    public static IReadOnlyList<string> Areas { get; } = Groups.Keys.SelectMany(g => new[] { $"{g}:read", $"{g}:write" }).ToList();

    /// <summary>The scopes of grants made before the seven areas.</summary>
    public static IReadOnlyList<string> Legacy { get; } =
    [
        ProfileRead, ProfileWrite, RecipesRead, RecipesWrite, HouseholdsRead, HouseholdsWrite,
        NotificationsRead, NotificationsWrite, TrainerRead, TrainerWrite, AiUse, Admin,
    ];

    /// <summary>Every scope a grant may hold: the areas, then the older scopes.</summary>
    public static IReadOnlyList<string> All { get; } = Areas.Concat(Legacy).ToList();

    public static bool IsKnown(string scope) => scope == Full || All.Contains(scope, StringComparer.Ordinal);

    /// <summary>Whether only an administrator may hold this scope.</summary>
    public static bool IsAdminScope(string scope) => scope is Admin or AdminRead or AdminWrite;

    /// <summary>
    /// Drops unknown scopes, makes write imply read, removes duplicates and
    /// keeps administration only when the user is an administrator.
    /// </summary>
    public static List<string> Normalize(IEnumerable<string>? scopes, bool allowAdmin)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in scopes ?? [])
        {
            var scope = raw.Trim();
            if (!All.Contains(scope, StringComparer.Ordinal)) continue;
            if (IsAdminScope(scope) && !allowAdmin) continue;
            set.Add(scope);
            if (scope.EndsWith(":write", StringComparison.Ordinal))
            {
                set.Add(scope[..^":write".Length] + ":read");
            }
        }
        return All.Where(set.Contains).ToList();
    }

    /// <summary>
    /// The older scopes an area took over: nutrition covers recipes and the AI estimates, planning
    /// covers households and reminders, social covers notifications, coaching is the trainer scope,
    /// and admin:write is the old admin scope.
    /// </summary>
    private static string[] Implied(string scope) => scope switch
    {
        NutritionRead => [RecipesRead],
        NutritionWrite => [RecipesWrite, RecipesRead, AiUse],
        PlanningRead => [HouseholdsRead, NotificationsRead],
        PlanningWrite => [HouseholdsWrite, HouseholdsRead, NotificationsWrite, NotificationsRead],
        SocialRead => [NotificationsRead],
        SocialWrite => [NotificationsWrite, NotificationsRead],
        CoachingRead => [TrainerRead],
        CoachingWrite => [TrainerWrite, TrainerRead],
        AdminWrite => [Admin],
        _ => [],
    };

    /// <summary>What a grant holding <paramref name="held"/> amounts to, with every implied scope added.</summary>
    public static HashSet<string> Expand(IEnumerable<string> held)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scope in held)
        {
            set.Add(scope);
            foreach (var implied in Implied(scope)) set.Add(implied);
        }

        // The profile belongs to no area: any area lets an app read it, and any area's write lets it change it.
        var areas = set.Where(s => Areas.Contains(s, StringComparer.Ordinal)).ToList();
        if (areas.Count > 0) set.Add(ProfileRead);
        if (areas.Any(s => s.EndsWith(":write", StringComparison.Ordinal))) set.Add(ProfileWrite);
        return set;
    }

    /// <summary>Whether a grant holding <paramref name="held"/> may do something that needs <paramref name="required"/>.</summary>
    public static bool Allows(IEnumerable<string> held, string required)
    {
        var heldList = held as ICollection<string> ?? held.ToList();
        if (heldList.Contains(Full))
        {
            return !IsAdminScope(required) || Allows(heldList.Where(s => s != Full).ToList(), required);
        }

        var set = Expand(heldList);
        if (set.Contains(required)) return true;
        if (required.EndsWith(":read", StringComparison.Ordinal))
        {
            return set.Contains(required[..^":read".Length] + ":write");
        }
        return false;
    }

    /// <summary>Parses the space separated form used on the wire.</summary>
    public static List<string> Parse(string? scope) =>
        string.IsNullOrWhiteSpace(scope)
            ? new List<string>()
            : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
}
