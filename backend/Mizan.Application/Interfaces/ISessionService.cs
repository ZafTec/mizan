namespace Mizan.Application.Interfaces;

/// <summary>
/// Browser sessions. The caller holds an opaque token; the database holds its
/// hash. Revocation is a delete, which is the whole reason v2 stopped issuing
/// JWTs to browsers - see docs/REFOCUS.md §6.
/// </summary>
public interface ISessionService
{
    /// <summary>Returns the plaintext token to put in the cookie. Stored hashed.</summary>
    Task<string> CreateAsync(Guid userId, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);

    /// <summary>The owning user, or null when the token is unknown or expired.</summary>
    Task<Guid?> ResolveAsync(string token, CancellationToken cancellationToken = default);

    Task RevokeAsync(string token, CancellationToken cancellationToken = default);

    Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs out every other browser but leaves the caller signed in. What a
    /// password change should do: kick the intruder, not the owner.
    /// </summary>
    Task RevokeAllExceptAsync(Guid userId, string keepToken, CancellationToken cancellationToken = default);

    /// <summary>Revokes one of the user's own sessions. Ignores ids they do not own.</summary>
    Task RevokeByIdAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
}
