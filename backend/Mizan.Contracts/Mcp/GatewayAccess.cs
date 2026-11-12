namespace Mizan.Contracts.Mcp;

/// <summary>
/// The hosted assistant's path into Mizan (docs/MCP.md#served-to-a-hosted-assistant-through-the-gateway). The MCP
/// service verifies the gateway's identity token and calls the API for that person with <see cref="Header"/> set; the
/// API marks the request with <see cref="Claim"/>, so the person's assistant consent applies and admin tools never do.
/// </summary>
public static class GatewayAccess
{
    /// <summary>Sent by the MCP service with the regular service key only. The API refuses it with the admin key or a connection.</summary>
    public const string Header = "X-Mcp-Gateway";

    /// <summary>The claim that marks a request as coming through the gateway, and its value.</summary>
    public const string Claim = "mcp_path";

    public const string Path = "gateway";
}
