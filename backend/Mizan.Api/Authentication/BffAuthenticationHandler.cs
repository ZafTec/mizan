using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Mizan.Api.Authentication;

public class BffAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    public string TrustedSecret { get; set; } = string.Empty;
}

public class BffAuthenticationHandler : AuthenticationHandler<BffAuthenticationSchemeOptions>
{
    private readonly ILogger<BffAuthenticationHandler> _logger;

    public BffAuthenticationHandler(
        IOptionsMonitor<BffAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        _logger = logger.CreateLogger<BffAuthenticationHandler>();
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for trusted secret header
        if (!Request.Headers.TryGetValue("X-BFF-Secret", out var secretValue))
        {
            _logger.LogWarning("Missing X-BFF-Secret header from {RemoteIp}", Request.HttpContext.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("Missing trusted secret header");
        }

        // Validate secret using constant-time comparison
        // SECURITY: Use FixedTimeEquals to prevent timing attacks.
        // Regular string comparison (==, !=) can leak information about the secret through
        // response timing variations. An attacker could measure how long comparisons take
        // and iteratively guess the secret byte-by-byte (timing side-channel attack).
        // FixedTimeEquals executes in constant time regardless of where strings differ,
        // preventing attackers from extracting secret information via timing analysis.
        var providedSecretBytes = Encoding.UTF8.GetBytes(secretValue.ToString());
        var trustedSecretBytes = Encoding.UTF8.GetBytes(Options.TrustedSecret);

        // Secrets must be same length for valid comparison
        if (providedSecretBytes.Length != trustedSecretBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(providedSecretBytes, trustedSecretBytes))
        {
            _logger.LogError("Invalid X-BFF-Secret from {RemoteIp}", Request.HttpContext.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("Invalid trusted secret");
        }

        // Extract user claims from BFF headers
        if (!Request.Headers.TryGetValue("X-User-Id", out var userIdValue) ||
            !Guid.TryParse(userIdValue, out var userId))
        {
            _logger.LogWarning("Missing or invalid X-User-Id header");
            return AuthenticateResult.Fail("Missing or invalid user ID");
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString())
        };

        // Optional: Email claim
        if (Request.Headers.TryGetValue("X-User-Email", out var emailValue))
        {
            claims.Add(new Claim(ClaimTypes.Email, emailValue!));
            claims.Add(new Claim("email", emailValue!));
        }

        // Optional: Role claim
        if (Request.Headers.TryGetValue("X-User-Role", out var roleValue))
        {
            claims.Add(new Claim(ClaimTypes.Role, roleValue!));
            claims.Add(new Claim("role", roleValue!));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        _logger.LogInformation("Authenticated user {UserId} via BFF", userId);

        return AuthenticateResult.Success(ticket);
    }
}
