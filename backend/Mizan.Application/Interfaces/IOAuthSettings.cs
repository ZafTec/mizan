namespace Mizan.Application.Interfaces;

/// <summary>A client we ship ourselves, such as the Android app. It may ask for API tokens and the full scope.</summary>
public sealed record FirstPartyClientConfig(string ClientId, string Name, IReadOnlyList<string> RedirectUris);

/// <summary>How long each OAuth artefact lives, and whether metadata-document clients are accepted. Runtime settings.</summary>
public sealed record OAuthLifetimes(
    TimeSpan AccessToken,
    TimeSpan RefreshToken,
    TimeSpan Request,
    TimeSpan Code,
    bool AllowMetadataClients);

/// <summary>
/// The authorization server. Its URLs follow <c>App:PublicUrl</c>; its lifetimes are runtime settings
/// read per use (docs/ADMIN.md#runtime-settings).
/// </summary>
public interface IOAuthSettings
{
    /// <summary>Base URL of the authorization server, with no trailing slash. Endpoints sit under it.</summary>
    string Issuer { get; }

    /// <summary>The MCP endpoint URL. It is the audience of MCP access tokens (RFC 8707).</summary>
    string McpResource { get; }

    /// <summary>The web page that shows the consent screen.</summary>
    string ConsentUrl { get; }

    IReadOnlyList<FirstPartyClientConfig> FirstPartyClients { get; }

    Task<OAuthLifetimes> GetLifetimesAsync(CancellationToken cancellationToken = default);
}
