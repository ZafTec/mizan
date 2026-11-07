using Mizan.Contracts.Mcp;

namespace Mizan.Api.Middleware;

/// <summary>
/// Holds an administrator who is viewing the site as a user to looking, logging and the user's ordinary use. They
/// cannot change the account's credentials, sign out its other sessions, delete it, touch its billing, link or
/// unlink its Telegram, or grant an app access to it. Those would let a support session do lasting damage or take
/// the account over. Which routes are which is decided in <see cref="ImpersonationPolicy"/>; a write to a route
/// it does not know is refused too.
/// </summary>
public sealed class ImpersonationGuardMiddleware
{
    private readonly RequestDelegate _next;

    public ImpersonationGuardMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        if (context.User.FindFirst(ImpersonationClaims.Impersonator) is null || !ImpersonationPolicy.IsRefused(context.Request))
            return _next(context);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(new
        {
            errorCode = ImpersonationPolicy.ErrorCode,
            error = "This is not available while you are viewing the site as another user.",
        });
    }
}
