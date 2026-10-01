using Mizan.Contracts.Mcp;

namespace Mizan.Mcp.Server.Authorization;

/// <summary>What the current connection may do, read from the claims its token carried in.</summary>
public static class McpGrantAccess
{
    public static IReadOnlyList<string> Held(HttpContext? httpContext) =>
        (McpCallIdentity.Of(httpContext)?.FindFirst(GrantClaims.Scopes)?.Value ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

    public static bool Allowed(HttpContext? httpContext, string? requiredScope) =>
        requiredScope is null || McpScopes.Allows(Held(httpContext), requiredScope);

    public static Guid UserId(HttpContext? httpContext) =>
        Guid.TryParse(McpCallIdentity.Of(httpContext)?.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;

    public static Guid GrantId(HttpContext? httpContext) =>
        Guid.TryParse(McpCallIdentity.Of(httpContext)?.FindFirst(GrantClaims.GrantId)?.Value, out var id) ? id : Guid.Empty;

    /// <summary>The free plan's monthly cap, or null when the plan has none or the cap has not been reached.</summary>
    public static string? LimitReached(HttpContext? httpContext) =>
        int.TryParse(McpCallIdentity.Of(httpContext)?.FindFirst("mcp_usage_limit")?.Value, out var limit)
        && int.TryParse(McpCallIdentity.Of(httpContext)?.FindFirst("mcp_usage_used")?.Value, out var used)
        && used >= limit
            ? $"[MONTHLY LIMIT REACHED] The free plan includes {limit} MCP calls per month. Upgrade at https://mizan.zaftech.co/billing."
            : null;
}
