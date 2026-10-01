using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Identity;

public class AuthorizationServerOptions
{
    public const string SectionName = "OAuth";

    /// <summary>Empty means <c>{App:PublicUrl}/api</c>, which is where one-host production serves the API.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Empty means <c>{App:PublicUrl}/mcp</c>.</summary>
    public string McpResource { get; set; } = string.Empty;

    /// <summary>Empty means <c>{App:PublicUrl}/oauth/consent</c>.</summary>
    public string ConsentUrl { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 30;
    public int RequestMinutes { get; set; } = 10;
    public int CodeSeconds { get; set; } = 120;

    /// <summary>Accept clients whose id is the URL of a metadata document.</summary>
    public bool AllowMetadataClients { get; set; } = true;

    public List<FirstPartyClientOptions> FirstPartyClients { get; set; } = new();
}

public class FirstPartyClientOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> RedirectUris { get; set; } = new();
}

public sealed class OAuthSettings : IOAuthSettings
{
    private readonly AuthorizationServerOptions _options;
    private readonly string _publicUrl;

    public OAuthSettings(IOptions<AuthorizationServerOptions> options, IOptions<AppOptions> app)
    {
        _options = options.Value;
        _publicUrl = app.Value.PublicUrl.TrimEnd('/');
    }

    public string Issuer => Pick(_options.Issuer, _publicUrl + "/api");
    public string McpResource => Pick(_options.McpResource, _publicUrl + "/mcp");
    public string ConsentUrl => Pick(_options.ConsentUrl, _publicUrl + "/oauth/consent");
    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(_options.AccessTokenMinutes);
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenDays);
    public TimeSpan RequestLifetime => TimeSpan.FromMinutes(_options.RequestMinutes);
    public TimeSpan CodeLifetime => TimeSpan.FromSeconds(_options.CodeSeconds);
    public bool AllowMetadataClients => _options.AllowMetadataClients;

    public IReadOnlyList<FirstPartyClientConfig> FirstPartyClients =>
        _options.FirstPartyClients
            .Where(c => !string.IsNullOrWhiteSpace(c.ClientId) && c.RedirectUris.Count > 0)
            .Select(c => new FirstPartyClientConfig(c.ClientId, string.IsNullOrWhiteSpace(c.Name) ? c.ClientId : c.Name, c.RedirectUris))
            .ToList();

    private static string Pick(string configured, string fallback) =>
        string.IsNullOrWhiteSpace(configured) ? fallback : configured.TrimEnd('/');
}
