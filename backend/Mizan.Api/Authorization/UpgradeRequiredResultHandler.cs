using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Mizan.Api.Authorization;

/// <summary>
/// A signed-in user who fails only <see cref="ProRequirement"/> gets the same
/// 402 upgrade_required as a Pro gate inside a command, not a bare 403 - so
/// every client reads one status for "this needs Pro".
/// </summary>
public class UpgradeRequiredResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        var failedOnlyOnPro = authorizeResult.Forbidden
            && authorizeResult.AuthorizationFailure?.FailedRequirements.Any() == true
            && authorizeResult.AuthorizationFailure.FailedRequirements.All(r => r is ProRequirement);

        if (failedOnlyOnPro)
        {
            context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
            await context.Response.WriteAsJsonAsync(new
            {
                errorCode = "upgrade_required",
                error = "This is a Pro feature. Upgrade to use it."
            });
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
