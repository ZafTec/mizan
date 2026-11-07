using System.Security.Claims;

namespace Mizan.Mcp.Server.Authorization;

/// <summary>
/// Who is calling, for work that outlives the request. A task runs its tool in the
/// background after the HTTP response is sent, when the HttpContext is gone. The
/// identity is kept here instead, and flows into that background work with the
/// execution context.
/// </summary>
public static class McpCallIdentity
{
    private static readonly AsyncLocal<ClaimsPrincipal?> Holder = new();

    public static ClaimsPrincipal? Current
    {
        get => Holder.Value;
        set => Holder.Value = value;
    }

    /// <summary>The caller of this request, or of the task being run for it.</summary>
    public static ClaimsPrincipal? Of(HttpContext? httpContext) => Current ?? httpContext?.User;
}
