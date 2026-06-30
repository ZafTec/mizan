using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Mizan.Application.Interfaces;

namespace Mizan.Api.Authorization;

/// <summary>Authorization requirement satisfied only when the current user has an active Pro entitlement.</summary>
public class ProRequirement : IAuthorizationRequirement;

public class ProAuthorizationHandler : AuthorizationHandler<ProRequirement>
{
    private readonly IEntitlementService _entitlements;

    public ProAuthorizationHandler(IEntitlementService entitlements)
    {
        _entitlements = entitlements;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ProRequirement requirement)
    {
        var userIdValue = context.User.FindFirst("sub")?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return;
        }

        var entitlement = await _entitlements.GetAsync(userId);
        if (entitlement.IsPro)
        {
            context.Succeed(requirement);
        }
    }
}
