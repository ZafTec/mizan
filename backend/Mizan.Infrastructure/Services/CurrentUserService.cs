using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Mcp;

namespace Mizan.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }

    public string? Email => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("email")?.Value;

    public string? Role => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("role")?.Value;

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

    public GrantContext? Grant
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
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

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        return string.Equals(Role, role, StringComparison.OrdinalIgnoreCase);
    }
}
