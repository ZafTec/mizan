using System.Globalization;
using Mizan.Contracts.Mcp;

namespace Mizan.Mcp.Server.Authorization;

public static class McpToolText
{
    /// <summary>"log_body_measurement" becomes "Log Body Measurement".</summary>
    public static string Title(string toolName) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(toolName.Replace('_', ' '));

    /// <summary>Said to the model, which passes it on to the person. It names the exact thing to change.</summary>
    public static string NotAllowed(string toolName, string? scope)
    {
        var needs = scope switch
        {
            null => "This tool is not available to connected apps.",
            McpScopes.Admin => "It needs administrator access, which can only be granted by an administrator.",
            _ => $"It needs the \"{scope}\" permission.",
        };

        return $"This connection is not allowed to use {toolName}. {needs} " +
            "The user can change what this app may do in Mizan under More, then Connected apps, or connect it again.";
    }
}
