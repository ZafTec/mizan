using System.Security.Cryptography;
using System.Text;

namespace Mizan.Domain.Identity;

/// <summary>
/// The only cryptography this codebase writes: generate random bytes, store
/// their hash. Sessions and mailed one-time links both use it.
/// </summary>
public static class SecureToken
{
    private const int TokenBytes = 32;

    public static string Generate() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));

    /// <summary>Lowercase hex SHA-256. Fixed length, safe to index and compare.</summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
