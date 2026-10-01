using Mizan.Contracts.Mcp;

namespace Mizan.Application.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    string? Role { get; }
    string? IpAddress { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);

    /// <summary>The administrator behind this request when they are viewing the site as the user, otherwise null.</summary>
    Guid? ImpersonatorId { get; }

    /// <summary>When the impersonation session ends, for the banner that tells the administrator how long is left.</summary>
    DateTime? ImpersonationExpiresAt { get; }

    /// <summary>
    /// Set when the request acts for a connected app (an MCP client, the
    /// Android app) rather than for a signed-in browser. It carries what the
    /// user allowed that app. Null for the web app, which holds the user's
    /// full authority.
    /// </summary>
    GrantContext? Grant { get; }
}

/// <summary>What one user allowed one connected app to do, as loaded from the database for this request.</summary>
public sealed record GrantContext(
    Guid GrantId,
    Guid ClientRowId,
    IReadOnlyList<string> Scopes,
    string HouseholdMode,
    IReadOnlyList<Guid> HouseholdIds)
{
    public bool Allows(string scope) => McpScopes.Allows(Scopes, scope);

    /// <summary>
    /// Goes into cache keys. A connected app can see fewer households than its
    /// user, so a result cached for the web app must not be served to it.
    /// </summary>
    public static string KeyFor(GrantContext? grant) =>
        grant is null ? "web" : $"{grant.GrantId:N}.{grant.HouseholdMode}.{string.Join(',', grant.HouseholdIds)}";

    public bool AllowsHousehold(Guid householdId) => HouseholdMode switch
    {
        "all" => true,
        "selected" => HouseholdIds.Contains(householdId),
        _ => false,
    };
}
