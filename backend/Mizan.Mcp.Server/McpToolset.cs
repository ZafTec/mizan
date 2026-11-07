namespace Mizan.Mcp.Server;

/// <summary>
/// Which tools a deployment serves (<c>Mcp:Toolset</c>, docs/MCP.md#toolsets). <see cref="Legacy"/> is the
/// endpoint-shaped tools in <c>Tools/</c>, one per API route. <see cref="Tasks"/> is the task tools defined in
/// <c>Mizan.Application/Tools</c>: the list is the caller's manifest from <c>GET /api/tools</c> and a call goes
/// to <c>POST /api/tools/{name}/call</c>, so the API holds every rule and this service only forwards.
/// </summary>
public enum McpToolset
{
    Legacy,
    Tasks,
}

public static class McpToolsets
{
    public const string ConfigKey = "Mcp:Toolset";

    /// <summary>Legacy unless the setting says tasks. Anything else is a typo that should stop the service, not pick a toolset.</summary>
    public static McpToolset Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or "legacy" => McpToolset.Legacy,
        "tasks" => McpToolset.Tasks,
        _ => throw new InvalidOperationException($"{ConfigKey} is legacy or tasks, not '{value}'."),
    };

    public static string Name(McpToolset toolset) => toolset == McpToolset.Tasks ? "tasks" : "legacy";
}
