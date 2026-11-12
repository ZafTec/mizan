using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Mcp;

namespace Mizan.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HttpContext? _requestContext;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        // The request this scope serves. HybridCache runs a cancellable factory on the thread pool
        // without the caller's execution context, where the accessor answers null; a query that
        // reads the viewer inside its cache factory would then see an anonymous caller.
        _requestContext = httpContextAccessor.HttpContext;
    }

    private HttpContext? Context => _httpContextAccessor.HttpContext ?? _requestContext;

    public Guid? UserId
    {
        get
        {
            var userIdClaim = Context?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? Context?.User?.FindFirst("sub")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }

    public string? Email => Context?.User?.FindFirst(ClaimTypes.Email)?.Value
        ?? Context?.User?.FindFirst("email")?.Value;

    public string? Role => Context?.User?.FindFirst(ClaimTypes.Role)?.Value
        ?? Context?.User?.FindFirst("role")?.Value;

    public string? IpAddress => Context?.Connection?.RemoteIpAddress?.ToString();

    public GrantContext? Grant
    {
        get
        {
            var user = Context?.User;
            if (!Guid.TryParse(user?.FindFirst(GrantClaims.GrantId)?.Value, out var grantId)) return null;

            Guid.TryParse(user!.FindFirst(GrantClaims.Client)?.Value, out var clientRowId);
            var scopes = (user.FindFirst(GrantClaims.Scopes)?.Value ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var households = (user.FindFirst(GrantClaims.Households)?.Value ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(h => Guid.TryParse(h, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();

            return new GrantContext(
                grantId, clientRowId, scopes,
                user.FindFirst(GrantClaims.HouseholdMode)?.Value ?? "none", households);
        }
    }

    public bool ViaGateway =>
        string.Equals(Context?.User?.FindFirst(GatewayAccess.Claim)?.Value, GatewayAccess.Path, StringComparison.Ordinal);

    public Guid? ImpersonatorId =>
        Guid.TryParse(Context?.User?.FindFirst(ImpersonationClaims.Impersonator)?.Value, out var id) ? id : null;

    public DateTime? ImpersonationExpiresAt =>
        DateTime.TryParse(Context?.User?.FindFirst(ImpersonationClaims.Expires)?.Value,
            null, System.Globalization.DateTimeStyles.RoundtripKind, out var at) ? at : null;

    public bool IsAuthenticated => Context?.User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        return string.Equals(Role, role, StringComparison.OrdinalIgnoreCase);
    }
}
