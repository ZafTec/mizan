using Mizan.Contracts.Mcp;

namespace Mizan.Api.Middleware;

/// <summary>
/// Holds an administrator who is viewing the site as a user to looking, logging and the user's ordinary use. They
/// cannot change the account's credentials, sign out its other sessions, delete it, touch its billing, link or
/// unlink its Telegram, or grant an app access to it. Those would let a support session do lasting damage or take
/// the account over.
/// </summary>
public sealed class ImpersonationGuardMiddleware
{
    private static readonly string[] Blocked =
    [
        "/api/Auth",
        "/api/Subscriptions",
        "/api/Telegram",
        "/api/oauth",
        "/api/McpConnections",
        "/api/Devices",
    ];

    private static readonly string[] Allowed =
    [
        "/api/Auth/me",
        "/api/Auth/logout",
        "/api/Auth/impersonation/stop",
    ];

    private readonly RequestDelegate _next;

    public ImpersonationGuardMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        if (context.User.FindFirst(ImpersonationClaims.Impersonator) is null || !IsRefused(context.Request))
            return _next(context);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(new
        {
            errorCode = "impersonation_restricted",
            error = "This is not available while you are viewing the site as another user.",
        });
    }

    internal static bool IsRefused(HttpRequest request)
    {
        if (request.Path.StartsWithSegments("/api/Auth/external", StringComparison.OrdinalIgnoreCase)) return true;

        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method))
            return false;

        var path = request.Path;
        return RefusedPath(path);
    }

    private static bool RefusedPath(PathString path)
    {
        // Signing in through an outside provider would replace the session with whoever that provider names.
        if (path.StartsWithSegments("/api/Auth/external", StringComparison.OrdinalIgnoreCase)) return true;
        if (Allowed.Any(a => path.StartsWithSegments(a, StringComparison.OrdinalIgnoreCase))) return false;
        return Blocked.Any(b => path.StartsWithSegments(b, StringComparison.OrdinalIgnoreCase));
    }
}
