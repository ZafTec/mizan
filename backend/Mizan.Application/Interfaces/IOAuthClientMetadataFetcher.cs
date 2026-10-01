namespace Mizan.Application.Interfaces;

public sealed record OAuthClientMetadata(
    string ClientId,
    string Name,
    string? LogoUri,
    string? ClientUri,
    IReadOnlyList<string> RedirectUris);

/// <summary>
/// Downloads a client metadata document from the URL a client uses as its id.
/// The URL is chosen by whoever starts the flow, so the implementation must
/// treat it as hostile (docs/MCP.md#connect-with-oauth).
/// </summary>
public interface IOAuthClientMetadataFetcher
{
    Task<OAuthClientMetadata> FetchAsync(string clientIdUrl, CancellationToken cancellationToken);
}
