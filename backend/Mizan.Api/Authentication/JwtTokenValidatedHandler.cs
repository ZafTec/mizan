using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Mizan.Application.Interfaces;

namespace Mizan.Api.Authentication;

// Runs after token signature validation. Consults UserStatusService (which is
// HybridCache-backed) to cheaply short-circuit banned / deleted / unverified
// users without hitting the DB on every request.
public static class JwtTokenValidatedHandler
{
    public static async Task HandleAsync(TokenValidatedContext context)
    {
        var userIdValue = context.Principal?.FindFirst("sub")?.Value
            ?? context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            context.Fail("Invalid user id");
            return;
        }

        var userStatus = context.HttpContext.RequestServices
            .GetRequiredService<IUserStatusService>();
        var status = await userStatus.GetStatusAsync(userId, context.HttpContext.RequestAborted);

        if (!status.Exists)
        {
            context.Fail("User not found");
            return;
        }

        if (!status.EmailVerified)
        {
            context.Fail("Email not verified");
            return;
        }

        if (status.IsBanned)
        {
            context.Fail("User banned");
            return;
        }
    }
}
