namespace Mizan.Application.Interfaces;

/// <summary>A client we ship ourselves, such as the Android app. It may ask for API tokens and the full scope.</summary>
public sealed record FirstPartyClientConfig(string ClientId, string Name, IReadOnlyList<string> RedirectUris);

public interface IOAuthSettings
{
    /// <summary>Base URL of the authorization server, with no trailing slash. Endpoints sit under it.</summary>
    string Issuer { get; }

    /// <summary>The MCP endpoint URL. It is the audience of MCP access tokens (RFC 8707).</summary>
    string McpResource { get; }

    /// <summary>The web page that shows the consent screen.</summary>
    string ConsentUrl { get; }

    TimeSpan AccessTokenLifetime { get; }
    TimeSpan RefreshTokenLifetime { get; }
    TimeSpan RequestLifetime { get; }
    TimeSpan CodeLifetime { get; }
    bool AllowMetadataClients { get; }
    IReadOnlyList<FirstPartyClientConfig> FirstPartyClients { get; }
}
