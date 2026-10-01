using Mizan.Contracts.Mcp;

namespace Mizan.Mcp.Server.Authorization;

/// <summary>What the current connection may do, read from the claims its token carried in.</summary>
public static class McpGrantAccess
{
    public static IReadOnlyList<string> Held(HttpContext? httpContext) =>
        (httpContext?.User.FindFirst(GrantClaims.Scopes)?.Value ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

    public static bool Allowed(HttpContext? httpContext, string? requiredScope) =>
        requiredScope is null || McpScopes.Allows(Held(httpContext), requiredScope);

    public static Guid UserId(HttpContext? httpContext) =>
        Guid.TryParse(httpContext?.User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;

    public static Guid GrantId(HttpContext? httpContext) =>
        Guid.TryParse(httpContext?.User.FindFirst(GrantClaims.GrantId)?.Value, out var id) ? id : Guid.Empty;

    /// <summary>The free plan's monthly cap, or null when the plan has none or the cap has not been reached.</summary>
    public static string? LimitReached(HttpContext? httpContext) =>
        int.TryParse(httpContext?.User.FindFirst("mcp_usage_limit")?.Value, out var limit)
        && int.TryParse(httpContext?.User.FindFirst("mcp_usage_used")?.Value, out var used)
        && used >= limit
            ? $"[MONTHLY LIMIT REACHED] The free plan includes {limit} MCP calls per month. Upgrade at https://mizan.zaftech.co/billing."
            : null;
}
