using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Mcp;

namespace Mizan.Api.Authentication;

public class ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public string HeaderName { get; set; } = "X-Api-Key";
    public string ApiKey { get; set; } = string.Empty;
    public string AdminApiKey { get; set; } = string.Empty;
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>
{
    private readonly ILogger<ApiKeyAuthenticationHandler> _logger;
    private readonly IUserStatusService _userStatusService;
    private readonly IMizanDbContext _context;

    public const string GrantHeader = "X-Mcp-Grant";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IUserStatusService userStatusService,
        IMizanDbContext context)
        : base(options, logger, encoder)
    {
        _logger = logger.CreateLogger<ApiKeyAuthenticationHandler>();
        _userStatusService = userStatusService;
        _context = context;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var extractedApiKey))
        {
            return AuthenticateResult.NoResult();
        }

        if (string.IsNullOrWhiteSpace(Options.ApiKey))
        {
            _logger.LogWarning("API Key is not configured in the server.");
            return AuthenticateResult.Fail("Server configuration error");
        }

        var providedBytes = Encoding.UTF8.GetBytes(extractedApiKey.ToString());
        var regularBytes = Encoding.UTF8.GetBytes(Options.ApiKey);
        var adminBytes = Encoding.UTF8.GetBytes(Options.AdminApiKey);
        var isRegularKey = providedBytes.Length == regularBytes.Length && CryptographicOperations.FixedTimeEquals(providedBytes, regularBytes);
        var isAdminKey = adminBytes.Length > 0 && providedBytes.Length == adminBytes.Length && CryptographicOperations.FixedTimeEquals(providedBytes, adminBytes);
        if (!isRegularKey && !isAdminKey)
        {
            _logger.LogWarning("Invalid API Key provided.");
            return AuthenticateResult.Fail("Invalid API Key");
        }

        // Check for impersonation header
        if (Request.Headers.TryGetValue("X-Impersonate-User", out var userIdString) &&
            Guid.TryParse(userIdString, out var userId))
        {
            // Verify user exists and is active
            var status = await _userStatusService.GetStatusAsync(userId, Context.RequestAborted);
            if (!status.IsAllowed || (string.Equals(status.Role, "admin", StringComparison.OrdinalIgnoreCase) && !isAdminKey))
            {
                _logger.LogWarning("Impersonation failed for user {UserId}.", userId);
                return AuthenticateResult.Fail("Impersonated user invalid");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("sub", userId.ToString()),
                new Claim("type", "service_impersonation"),
                new Claim(ClaimTypes.Role, status.Role),
                new Claim("role", status.Role),
                // Add standard role claim for Identity
                new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", status.Role)
            };

            // The MCP service names the connection a call came from. What that
            // connection may do is read from the database here, never taken from
            // the header, so the service cannot widen it by asking.
            if (Request.Headers.TryGetValue(GrantHeader, out var grantHeader))
            {
                if (!Guid.TryParse(grantHeader, out var grantId))
                    return AuthenticateResult.Fail("Invalid connection");

                var grant = await _context.OAuthGrants.AsNoTracking()
                    .FirstOrDefaultAsync(g => g.Id == grantId && g.UserId == userId && g.RevokedAt == null, Context.RequestAborted);
                if (grant is null)
                {
                    _logger.LogWarning("Rejected a call for connection {GrantId}: not found, revoked or not this user's.", grantId);
                    return AuthenticateResult.Fail("The connection is no longer valid");
                }

                var isAdminUser = string.Equals(status.Role, "admin", StringComparison.OrdinalIgnoreCase);
                var scopes = isAdminUser ? grant.Scopes : grant.Scopes.Where(s => s != McpScopes.Admin).ToList();
                claims.Add(new Claim(GrantClaims.GrantId, grant.Id.ToString()));
                claims.Add(new Claim(GrantClaims.Client, grant.ClientId.ToString()));
                claims.Add(new Claim(GrantClaims.Scopes, string.Join(' ', scopes)));
                claims.Add(new Claim(GrantClaims.HouseholdMode, grant.HouseholdMode));
                claims.Add(new Claim(GrantClaims.Households, string.Join(',', grant.HouseholdIds)));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.NameIdentifier, ClaimTypes.Role);
            var principal = new ClaimsPrincipal(identity);

            _logger.LogInformation("ApiKey Impersonation Success. User: {UserId}, Role: {Role}, Claims: {Claims}",
                userId, status.Role, string.Join(", ", claims.Select(c => $"{c.Type}={c.Value}")));

            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }

        // If no impersonation, just authenticate as "Service"
        var serviceClaims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "service"),
            new Claim("type", "service")
        };
        var serviceIdentity = new ClaimsIdentity(serviceClaims, Scheme.Name);
        var servicePrincipal = new ClaimsPrincipal(serviceIdentity);
        var serviceTicket = new AuthenticationTicket(servicePrincipal, Scheme.Name);

        return AuthenticateResult.Success(serviceTicket);
    }
}
