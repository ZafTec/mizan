using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Domain.Identity;

namespace Mizan.Application.OAuth;

/// <summary>Token shapes and issuing. The prefixes make a leaked token easy for secret scanners to find.</summary>
public static class OAuthTokens
{
    public const string AccessPrefix = "mza_";
    public const string RefreshPrefix = "mzr_";

    public static string Hash(string token) => SecureToken.Hash(token);

    public sealed record Issued(string AccessToken, string RefreshToken, DateTime AccessExpiresAt);

    /// <summary>Stages a new access and refresh token. The caller saves.</summary>
    public static Issued Issue(
        IMizanDbContext context, IOAuthSettings settings, Guid grantId, Guid familyId, string audience, DateTime now)
    {
        var access = AccessPrefix + SecureToken.Generate();
        var refresh = RefreshPrefix + SecureToken.Generate();
        var accessExpires = now + settings.AccessTokenLifetime;

        context.OAuthTokens.Add(new OAuthToken
        {
            Id = Guid.CreateVersion7(), GrantId = grantId, FamilyId = familyId, Kind = OAuthToken.KindAccess,
            Audience = audience, TokenHash = Hash(access), CreatedAt = now, ExpiresAt = accessExpires,
        });
        context.OAuthTokens.Add(new OAuthToken
        {
            Id = Guid.CreateVersion7(), GrantId = grantId, FamilyId = familyId, Kind = OAuthToken.KindRefresh,
            Audience = audience, TokenHash = Hash(refresh), CreatedAt = now, ExpiresAt = now + settings.RefreshTokenLifetime,
        });

        return new Issued(access, refresh, accessExpires);
    }
}
