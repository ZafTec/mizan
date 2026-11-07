using System.Text.Json;
using System.Text.Json.Nodes;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Mcp;
using Mizan.Contracts.Tools;

namespace Mizan.Application.Tools;

/// <summary>
/// Which modes a caller may use: by role first, then by surface, then by what the connection was
/// granted. The manifest and every call go through the same check, so a mode that is not listed
/// cannot be called either (docs/MCP.md#what-a-connection-can-see-and-call).
/// </summary>
public static class ToolFilter
{
    public static bool RoleAllows(ToolRole minimum, string role) => minimum switch
    {
        ToolRole.User => true,
        ToolRole.Trainer => string.Equals(role, "trainer", StringComparison.OrdinalIgnoreCase),
        ToolRole.Admin => string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    /// <summary>
    /// Whether the connection covers the mode. A grant stored in areas needs the mode's area; a grant
    /// made before the areas also reaches the mode through the scope of any legacy tool it replaces,
    /// so no older connection loses what it could do and none gains a neighbouring area.
    /// No grant (the web app, the assistant in the app) holds the person's full authority.
    /// </summary>
    public static bool GrantAllows(ToolMode mode, GrantContext? grant)
    {
        if (grant is null) return true;
        var held = grant.Scopes;
        if (mode.Area == ToolMode.AnyConnection) return true;
        if (mode.Area == ToolMode.AnyWrite)
        {
            return McpScopes.Allows(held, McpScopes.ProfileWrite);
        }

        if (McpScopes.Allows(held, mode.Area)) return true;

        // A grant made before the areas reaches the mode through an older scope it holds itself,
        // never through one an area implies: nutrition:write implies ai:use for the legacy tools,
        // and must not open a planning mode that replaced an ai:use tool.
        foreach (var legacy in mode.Replaces)
        {
            if (!McpToolScopes.TryGet(legacy, out var scope) || McpScopes.Areas.Contains(scope)) continue;
            if (held.Contains(scope)) return true;
            if (scope.EndsWith(":read", StringComparison.Ordinal) && held.Contains(scope[..^":read".Length] + ":write")) return true;
        }

        return false;
    }

    /// <summary>What an administrator viewing Mizan as someone else is missing for an admin mode.</summary>
    public const string NotImpersonating = "not_impersonating";

    /// <summary>Null when the caller may use the mode, else the scope it is missing (or the role, as <c>role:trainer</c>).</summary>
    public static string? Refusal(ToolMode mode, ToolCallContext context)
    {
        // Seeing the site as someone else never reaches administration, whatever role the session holds.
        if (mode.MinimumRole == ToolRole.Admin && context.Impersonating) return NotImpersonating;
        if (!RoleAllows(mode.MinimumRole, context.Role)) return $"role:{mode.MinimumRole.ToString().ToLowerInvariant()}";
        if (!mode.Surfaces.HasFlag(context.Surface)) return $"surface:{SurfaceName(context.Surface)}";
        return GrantAllows(mode, context.Grant) ? null : mode.Area;
    }

    public static IReadOnlyList<ToolMode> Allowed(TaskTool tool, ToolCallContext context)
    {
        var allowed = tool.Modes.Where(m => Refusal(m, context) is null).ToList();
        // Confirming is offered only to a caller who can preview something to confirm.
        if (!allowed.Any(m => m.Confirm is not null)) allowed.RemoveAll(m => m.Name == TaskToolCatalogue.ConfirmMode);
        return allowed;
    }

    public static string SurfaceName(ToolSurface surface) =>
        surface == ToolSurface.Assistant ? ToolSurfaces.Assistant : ToolSurfaces.Mcp;

    public static ToolSurface? ParseSurface(string? surface) => surface?.Trim().ToLowerInvariant() switch
    {
        null or "" or ToolSurfaces.Mcp => ToolSurface.Mcp,
        ToolSurfaces.Assistant => ToolSurface.Assistant,
        _ => null,
    };

    /// <summary>The tool as this caller sees it, with its mode enum narrowed, or null when no mode is left.</summary>
    public static ToolDescriptorDto? Describe(TaskTool tool, ToolCallContext context)
    {
        var modes = Allowed(tool, context);
        if (modes.Count == 0) return null;
        return new ToolDescriptorDto(
            tool.Name,
            tool.Title,
            tool.Description,
            KindName(tool.Kind),
            JsonSerializer.SerializeToElement(Schema(tool, modes)),
            context.Surface == ToolSurface.Mcp ? tool.App : null);
    }

    public static string KindName(ToolKind kind) => kind switch
    {
        ToolKind.Read => ToolKinds.Read,
        ToolKind.Write => ToolKinds.Write,
        _ => ToolKinds.Destructive,
    };

    /// <summary>The input schema with the mode argument listing only <paramref name="modes"/>.</summary>
    public static JsonObject Schema(TaskTool tool, IReadOnlyList<ToolMode> modes)
    {
        var properties = new JsonObject();
        if (tool.ModeArgument is { } argument)
        {
            var lines = tool.ModeHelp ?? string.Join("\n", modes.Select(m => $"{m.Name}: {m.Summary}"));
            var mode = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray(modes.Select(m => (JsonNode)JsonValue.Create(m.Name)!).ToArray()),
                ["description"] = lines,
            };
            if (tool.DefaultMode is { } fallback && modes.Any(m => m.Name == fallback)) mode["default"] = fallback;
            properties[argument] = mode;
        }

        foreach (var (name, schema) in tool.Properties)
        {
            properties[name] = schema?.DeepClone();
        }

        var required = new JsonArray();
        if (tool.ModeArgument is { } modeArgument && (tool.DefaultMode is null || modes.All(m => m.Name != tool.DefaultMode)))
            required.Add(modeArgument);
        foreach (var name in tool.Required) required.Add(name);

        var root = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false,
        };
        if (required.Count > 0) root["required"] = required;
        OptionalMayBeNull(root);
        return root;
    }

    /// <summary>
    /// Every property an object does not require also takes null, at any depth. A client that fills
    /// every property (as strict tool calling does) then sends null for what it does not mean, not a
    /// zero or an empty string that reads as a value.
    /// </summary>
    private static void OptionalMayBeNull(JsonObject schema)
    {
        if (schema["properties"] is JsonObject properties)
        {
            var required = (schema["required"] as JsonArray)?.Select(r => r?.GetValue<string>()).ToHashSet() ?? [];
            foreach (var (name, property) in properties.ToList())
            {
                if (property is not JsonObject child) continue;
                OptionalMayBeNull(child);
                if (!required.Contains(name)) TakesNull(child);
            }
        }
        if (schema["items"] is JsonObject items) OptionalMayBeNull(items);
    }

    private static void TakesNull(JsonObject schema)
    {
        switch (schema["type"])
        {
            case JsonValue single when single.GetValue<string>() != "null":
                schema["type"] = new JsonArray(single.GetValue<string>(), "null");
                break;
            case JsonArray types when !types.Any(t => t?.GetValue<string>() == "null"):
                types.Add("null");
                break;
        }
        if (schema["enum"] is JsonArray values && !values.Any(v => v is null)) values.Add(null);
    }
}
