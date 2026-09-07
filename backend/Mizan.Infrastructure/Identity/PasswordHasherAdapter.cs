using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using NSec.Cryptography;

namespace Mizan.Infrastructure.Identity;

/// <summary>
/// Wraps ASP.NET Core Identity's PasswordHasher, which is available standalone -
/// no user store, no AspNet* tables. PBKDF2-HMAC-SHA512 with the framework's
/// current iteration count, and a versioned format that survives it changing.
/// </summary>
public class PasswordHasherAdapter : IPasswordHasher
{
    // The v1 import adds this tag to Better Auth's original salt:key value.
    // Untagged values belong to ASP.NET Identity; new hashes always use it.
    public const string LegacyBetterAuthPrefix = "betterauth-scrypt$";

    private readonly PasswordHasher<User> _hasher = new();
    private static readonly User Subject = new();
    private static readonly Lazy<Scrypt> LegacyScrypt = new(() => new Scrypt(new ScryptParameters
    {
        Cost = 16384,
        BlockSize = 16,
        Parallelization = 1,
    }));

    public string Hash(string password) => _hasher.HashPassword(Subject, password);

    public bool Verify(string hash, string password) => Verify(hash, password, out _);

    public bool Verify(string hash, string password, out bool needsRehash)
    {
        needsRehash = false;
        if (string.IsNullOrEmpty(hash) || password is null) return false;

        try
        {
            if (hash.StartsWith(LegacyBetterAuthPrefix, StringComparison.Ordinal))
            {
                needsRehash = VerifyLegacy(hash.AsSpan(LegacyBetterAuthPrefix.Length), password);
                return needsRehash;
            }

            var result = _hasher.VerifyHashedPassword(Subject, hash, password);
            needsRehash = result == PasswordVerificationResult.SuccessRehashNeeded;
            return result is PasswordVerificationResult.Success
                or PasswordVerificationResult.SuccessRehashNeeded;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            // Includes malformed Unicode that cannot be normalized.
            return false;
        }
    }

    private static bool VerifyLegacy(ReadOnlySpan<char> hash, string password)
    {
        if (hash.Length != 161 || hash[32] != ':'
            || !IsLowerHex(hash[..32]) || !IsLowerHex(hash[33..])) return false;

        // Better Auth 1.6.23 / @better-auth/utils 0.4.2 uses NFKC and passes
        // the hexadecimal salt as UTF-8 text, rather than decoding its bytes.
        var salt = Encoding.UTF8.GetBytes(hash[..32].ToString());
        var expected = Convert.FromHexString(hash[33..]);
        // NSec's string overload uses a different encoding; pass UTF-8 bytes.
        var passwordBytes = Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormKC));
        Span<byte> actual = stackalloc byte[64];
        try
        {
            LegacyScrypt.Value.DeriveBytes(passwordBytes, salt, actual);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            CryptographicOperations.ZeroMemory(actual);
        }
    }

    private static bool IsLowerHex(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')) return false;
        }
        return true;
    }
}
