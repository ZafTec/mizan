using MediatR;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Domain.Identity;

namespace Mizan.Application.OAuth;

/// <summary>Dynamic client registration (RFC 7591). Only public clients: there is no secret to keep.</summary>
public record RegisterOAuthClientCommand(
    string? ClientName,
    IReadOnlyList<string>? RedirectUris,
    string? LogoUri,
    string? ClientUri,
    string? TokenEndpointAuthMethod,
    IReadOnlyList<string>? GrantTypes,
    IReadOnlyList<string>? ResponseTypes) : IRequest<OAuthClientRegistration>, ISkipAudit;

public record OAuthClientRegistration(
    string ClientId,
    long ClientIdIssuedAt,
    string ClientName,
    IReadOnlyList<string> RedirectUris);

public class RegisterOAuthClientCommandHandler : IRequestHandler<RegisterOAuthClientCommand, OAuthClientRegistration>
{
    private const int MaxRedirectUris = 10;
    private readonly IMizanDbContext _context;

    public RegisterOAuthClientCommandHandler(IMizanDbContext context) => _context = context;

    public async Task<OAuthClientRegistration> Handle(RegisterOAuthClientCommand request, CancellationToken cancellationToken)
    {
        var redirects = (request.RedirectUris ?? []).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();
        if (redirects.Count == 0)
            throw new OAuthException("invalid_redirect_uri", "At least one redirect URI is required.");
        if (redirects.Count > MaxRedirectUris)
            throw new OAuthException("invalid_redirect_uri", $"At most {MaxRedirectUris} redirect URIs are allowed.");
        foreach (var uri in redirects)
        {
            if (!RedirectUriRules.TryValidate(uri, out var reason))
                throw new OAuthException("invalid_redirect_uri", reason);
        }

        if (request.TokenEndpointAuthMethod is not (null or "none"))
            throw new OAuthException("invalid_client_metadata", "Only public clients are supported (token_endpoint_auth_method: none).");
        if (request.GrantTypes is { Count: > 0 } && request.GrantTypes.Any(g => g is not ("authorization_code" or "refresh_token")))
            throw new OAuthException("invalid_client_metadata", "Only the authorization_code and refresh_token grant types are supported.");
        if (request.ResponseTypes is { Count: > 0 } && request.ResponseTypes.Any(r => r != "code"))
            throw new OAuthException("invalid_client_metadata", "Only the code response type is supported.");

        var name = string.IsNullOrWhiteSpace(request.ClientName) ? "Unnamed app" : request.ClientName.Trim();
        if (name.Length > 100) name = name[..100];

        var logo = HttpsOrNull(request.LogoUri);
        var clientUri = HttpsOrNull(request.ClientUri);
        var now = DateTime.UtcNow;

        var client = new OAuthClient
        {
            Id = Guid.CreateVersion7(),
            ClientId = "mzc_" + SecureToken.Generate()[..22],
            Name = name,
            LogoUri = logo,
            ClientUri = clientUri,
            RedirectUris = redirects,
            Source = OAuthClient.SourceDynamic,
            CreatedAt = now,
        };

        _context.OAuthClients.Add(client);
        await _context.SaveChangesAsync(cancellationToken);

        return new OAuthClientRegistration(client.ClientId, new DateTimeOffset(now).ToUnixTimeSeconds(), client.Name, client.RedirectUris);
    }

    private static string? HttpsOrNull(string? value) =>
        value is { Length: <= 512 } && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? value
            : null;
}
