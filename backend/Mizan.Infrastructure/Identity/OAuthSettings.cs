using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Identity;

/// <summary>The OAuth runtime settings (docs/ADMIN.md#runtime-settings).</summary>
public class AuthorizationServerOptions
{
    public const string SectionName = "OAuth";

    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 30;
    public int RequestMinutes { get; set; } = 10;
    public int CodeSeconds { get; set; } = 120;

    /// <summary>Accept clients whose id is the URL of a metadata document.</summary>
    public bool AllowMetadataClients { get; set; } = true;
}

/// <summary>
/// The issuer, MCP resource and consent page sit under <c>App:PublicUrl</c>, where one-host
/// production serves the API (/api), the MCP server (/mcp) and the web app. First-party clients are
/// the apps we ship, so they are code rather than settings.
/// </summary>
public sealed class OAuthSettings : IOAuthSettings
{
    /// <summary>The apps we ship. A new one is a code change, reviewed with its redirect URIs.</summary>
    public static readonly IReadOnlyList<FirstPartyClientConfig> ShippedClients =
    [
        new("mizan-android", "Mizan for Android", ["com.zaftech.mizan:/oauth2redirect"]),
    ];

    private readonly IRuntimeOptions<AuthorizationServerOptions> _options;
    private readonly string _publicUrl;

    public OAuthSettings(IRuntimeOptions<AuthorizationServerOptions> options, IOptions<AppOptions> app)
    {
        _options = options;
        _publicUrl = app.Value.PublicUrl.TrimEnd('/');
    }

    public string Issuer => _publicUrl + "/api";
    public string McpResource => _publicUrl + "/mcp";
    public string ConsentUrl => _publicUrl + "/oauth/consent";
    public IReadOnlyList<FirstPartyClientConfig> FirstPartyClients => ShippedClients;

    public async Task<OAuthLifetimes> GetLifetimesAsync(CancellationToken cancellationToken = default)
    {
        var options = await _options.GetAsync(cancellationToken);
        return new OAuthLifetimes(
            TimeSpan.FromMinutes(options.AccessTokenMinutes),
            TimeSpan.FromDays(options.RefreshTokenDays),
            TimeSpan.FromMinutes(options.RequestMinutes),
            TimeSpan.FromSeconds(options.CodeSeconds),
            options.AllowMetadataClients);
    }
}
