using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Extensions.Apps;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Apps;

/// <summary>
/// The tools behind a widget answer with <c>structuredContent</c>, the MCP Apps field a UI reads its data from,
/// next to the JSON text the model reads. A host adds text blocks of its own to a result before the widget sees
/// it, so the text alone is not a reliable source.
/// </summary>
public static class WidgetResults
{
    /// <summary>Every tool with an <see cref="McpAppUiAttribute"/>.</summary>
    public static IReadOnlySet<string> Tools { get; } = typeof(WidgetResults).Assembly.GetTypes()
        .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        .Where(method => method.GetCustomAttribute<McpAppUiAttribute>() is not null)
        .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
        .OfType<string>()
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Adds <c>structuredContent</c> from the result's first JSON text block, unless the result is an error or
    /// already has it. <c>structuredContent</c> is an object, so a JSON array arrives as <c>{ "items": [...] }</c>.
    /// </summary>
    public static CallToolResult WithStructuredContent(string toolName, CallToolResult result)
    {
        if (result.IsError == true || result.StructuredContent is not null || !Tools.Contains(toolName)) return result;

        foreach (var block in result.Content.OfType<TextContentBlock>())
        {
            JsonElement value;
            try { value = JsonSerializer.Deserialize<JsonElement>(block.Text); }
            catch (JsonException) { continue; }

            if (value.ValueKind == JsonValueKind.Object)
            {
                result.StructuredContent = value;
                return result;
            }
            if (value.ValueKind == JsonValueKind.Array)
            {
                result.StructuredContent = JsonSerializer.SerializeToElement(new Dictionary<string, JsonElement> { ["items"] = value });
                return result;
            }
        }
        return result;
    }
}
