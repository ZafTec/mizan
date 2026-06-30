using System.Security.Cryptography;
using System.Text;

namespace Mizan.Api.Webhooks;

/// <summary>
/// Verifies Paddle webhook signatures. Paddle sends a `Paddle-Signature` header of the
/// form `ts=&lt;unix&gt;;h1=&lt;hex&gt;`. The signed payload is `&lt;ts&gt;:&lt;rawBody&gt;`,
/// HMAC-SHA256 with the destination secret key, hex-encoded.
/// </summary>
public static class PaddleSignatureVerifier
{
    public static bool Verify(string rawBody, string signatureHeader, string secret, int maxAgeSeconds = 300)
    {
        if (string.IsNullOrEmpty(signatureHeader) || string.IsNullOrEmpty(secret))
        {
            return false;
        }

        string? ts = null;
        string? h1 = null;
        foreach (var part in signatureHeader.Split(';'))
        {
            var idx = part.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = part[..idx].Trim();
            var value = part[(idx + 1)..].Trim();
            if (key == "ts") ts = value;
            else if (key == "h1") h1 = value;
        }

        if (ts is null || h1 is null)
        {
            return false;
        }

        if (maxAgeSeconds > 0 && long.TryParse(ts, out var tsUnix))
        {
            var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - tsUnix;
            if (age > maxAgeSeconds || age < -maxAgeSeconds)
            {
                return false;
            }
        }

        var signedPayload = $"{ts}:{rawBody}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload)));

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(h1));
    }
}
