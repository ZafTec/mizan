using Microsoft.AspNetCore.Identity;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Identity;

/// <summary>
/// Wraps ASP.NET Core Identity's PasswordHasher, which is available standalone -
/// no user store, no AspNet* tables. PBKDF2-HMAC-SHA512 with the framework's
/// current iteration count, and a versioned format that survives it changing.
/// </summary>
public class PasswordHasherAdapter : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();
    private static readonly User Subject = new();

    public string Hash(string password) => _hasher.HashPassword(Subject, password);

    public bool Verify(string hash, string password)
    {
        var result = _hasher.VerifyHashedPassword(Subject, hash, password);
        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
