using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Mizan.Api.Authentication;

namespace Mizan.Api.Middleware;

/// <summary>
/// Refuses a write that a page on another site made with the person's session cookie
/// (docs/ARCHITECTURE.md#cross-site-writes). The verdict is the framework's own
/// <see cref="ICsrfProtection"/>: Sec-Fetch-Site, else Origin, with the credentialed CORS policy's
/// origins trusted. A request without either header (the web server's own calls, Telegram, a
/// script) passes, and a caller that signs with a key or bearer token is not held to it, because
/// a page on another site cannot borrow those.
/// </summary>
public sealed class CrossSiteWriteFilter : IAsyncAuthorizationFilter
{
    public const string ErrorCode = "cross_site_request";

    private readonly ICsrfProtection _csrf;

    public CrossSiteWriteFilter(ICsrfProtection csrf) => _csrf = csrf;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        if (IsSafe(http.Request.Method)
            || http.User.Identity is not { IsAuthenticated: true, AuthenticationType: SessionCookieAuthenticationSchemeOptions.DefaultScheme })
            return;

        if ((await _csrf.ValidateAsync(http)).IsAllowed) return;

        context.Result = new ObjectResult(new
        {
            errorCode = ErrorCode,
            error = "This request came from another site and was not accepted.",
        })
        { StatusCode = StatusCodes.Status403Forbidden };
    }

    private static bool IsSafe(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);
}
