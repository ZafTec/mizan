using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using NSec.Cryptography;

namespace Mizan.Api.Authentication;

public static class EdDsaJwtSignatureValidator
{
    private static readonly JwtSecurityTokenHandler TokenHandler = new();

    public static SecurityToken Validate(string token, TokenValidationParameters parameters)
    {
        try
        {
            var jwt = TokenHandler.ReadJwtToken(token);
            var alg = jwt.Header.Alg;

            if (!string.Equals(alg, "EdDSA", StringComparison.Ordinal))
            {
                throw new SecurityTokenInvalidSignatureException($"Unsupported JWT algorithm '{alg}'.");
            }

            var provider = JwksProviderAccessor.Current;
            if (provider == null)
            {
                throw new SecurityTokenInvalidSignatureException("JWKS provider not configured.");
            }

            var keys = provider.GetSigningKeysAsync(CancellationToken.None).GetAwaiter().GetResult();
            var jwksKeys = keys.OfType<JsonWebKey>();

            if (!string.IsNullOrWhiteSpace(jwt.Header.Kid))
            {
                jwksKeys = jwksKeys.Where(k => string.Equals(k.Kid, jwt.Header.Kid, StringComparison.Ordinal));
            }

            var signingInput = Encoding.ASCII.GetBytes($"{jwt.RawHeader}.{jwt.RawPayload}");
            var signature = Base64UrlEncoder.DecodeBytes(jwt.RawSignature);

            foreach (var jwk in jwksKeys)
            {
                if (!IsEd25519Key(jwk) || string.IsNullOrWhiteSpace(jwk.X))
                {
                    continue;
                }

                var publicKeyBytes = Base64UrlEncoder.DecodeBytes(jwk.X);
                var publicKey = PublicKey.Import(SignatureAlgorithm.Ed25519, publicKeyBytes, KeyBlobFormat.RawPublicKey);

                if (SignatureAlgorithm.Ed25519.Verify(publicKey, signingInput, signature))
                {
                    return jwt;
                }
            }

            throw new SecurityTokenInvalidSignatureException("JWT signature validation failed.");
        }
        catch (SecurityTokenException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SecurityTokenInvalidSignatureException("JWT signature validation failed.", ex);
        }
    }

    private static bool IsEd25519Key(JsonWebKey key)
    {
        if (!string.Equals(key.Kty, "OKP", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(key.Crv, "Ed25519", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(key.Alg) && !string.Equals(key.Alg, "EdDSA", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
