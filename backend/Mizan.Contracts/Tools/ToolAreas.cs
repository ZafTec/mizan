using Mizan.Contracts.Mcp;

namespace Mizan.Contracts.Tools;

/// <summary>
/// The seven areas of the task tools (docs/MCP.md#what-a-connection-can-see-and-call), and how a
/// grant made before them reads as areas.
/// </summary>
public static class ToolAreas
{
    public const string Nutrition = "nutrition";
    public const string Training = "training";
    public const string Body = "body";
    public const string Planning = "planning";
    public const string Social = "social";
    public const string Coaching = "coaching";
    public const string Admin = "admin";

    public static readonly IReadOnlyList<string> All = [Nutrition, Training, Body, Planning, Social, Coaching, Admin];

    public static string Read(string area) => $"{area}:read";
    public static string Write(string area) => $"{area}:write";

    /// <summary>
    /// The older scope whose holder reads as holding an area: recipes counts as nutrition,
    /// households as planning, notifications as social, trainer as coaching and admin as admin.
    /// Used to show an older grant on the seven-area screens; what a tool may do is still decided
    /// mode by mode (a recipes-only grant reaches the recipe modes, not the diary).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Older = new Dictionary<string, string>
    {
        [Nutrition] = "recipes",
        [Planning] = "households",
        [Social] = "notifications",
        [Coaching] = "trainer",
    };

    /// <summary>A grant's scopes as the area scopes it reads as, read before write, in the screens' order.</summary>
    public static List<string> FromScopes(IEnumerable<string> scopes)
    {
        var held = scopes as ICollection<string> ?? scopes.ToList();
        var result = new List<string>();
        foreach (var area in All)
        {
            var read = McpScopes.Allows(held, Read(area));
            var write = McpScopes.Allows(held, Write(area));
            if (area == Admin && held.Contains(McpScopes.Admin)) { read = write = true; }
            if (Older.TryGetValue(area, out var older))
            {
                read |= held.Contains($"{older}:read") || held.Contains($"{older}:write");
                write |= held.Contains($"{older}:write");
            }

            if (read || write) result.Add(Read(area));
            if (write) result.Add(Write(area));
        }

        return result;
    }
}
