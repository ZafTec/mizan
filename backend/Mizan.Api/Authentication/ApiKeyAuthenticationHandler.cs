using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;

namespace Mizan.Api.Authentication;

public class ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public string HeaderName { get; set; } = "X-Api-Key";
    public string ApiKey { get; set; } = string.Empty;
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>
{
    private readonly ILogger<ApiKeyAuthenticationHandler> _logger;
    private readonly IUserStatusService _userStatusService;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IUserStatusService userStatusService)
        : base(options, logger, encoder)
    {
        _logger = logger.CreateLogger<ApiKeyAuthenticationHandler>();
        _userStatusService = userStatusService;
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

        if (!string.Equals(extractedApiKey, Options.ApiKey, StringComparison.Ordinal))
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
            if (!status.Exists || status.IsBanned)
            {
                _logger.LogWarning("Impersonation failed: User {UserId} not found or banned.", userId);
                return AuthenticateResult.Fail("Impersonated user invalid");
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("sub", userId.ToString()),
                new Claim("type", "service_impersonation"),
                new Claim(ClaimTypes.Role, status.Role),
                new Claim("role", status.Role),
                // Add standard role claim for Identity
                new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", status.Role)
            };

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
