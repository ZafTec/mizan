using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Domain.Identity;

namespace Mizan.Application.Auth;

internal static class AuthTokens
{
    /// <summary>
    /// Issues a fresh one-time token and invalidates any earlier one for the
    /// same purpose, so a second "resend" cannot leave two live links.
    ///
    /// Staged, not saved: the caller commits it together with the queued email
    /// that carries it, so a link can never exist without a mail on its way or
    /// the other way round.
    /// </summary>
    public static async Task<string> IssueAsync(
        IMizanDbContext context,
        Guid userId,
        UserTokenPurpose purpose,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        await context.UserTokens
            .Where(t => t.UserId == userId && t.Purpose == purpose && t.ConsumedAt == null)
            .ExecuteDeleteAsync(cancellationToken);

        var token = SecureToken.Generate();
        var now = DateTime.UtcNow;
        context.UserTokens.Add(new UserToken
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Purpose = purpose,
            TokenHash = SecureToken.Hash(token),
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime),
        });

        return token;
    }

    /// <summary>
    /// Marks the token used and returns it. Throws when it is unknown, expired
    /// or already consumed - all three are the same message to the caller.
    /// </summary>
    public static async Task<UserToken> ConsumeAsync(
        IMizanDbContext context,
        UserTokenPurpose purpose,
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new DomainValidationException("That link is not valid. Request a new one.");
        }

        var hash = SecureToken.Hash(token);
        var record = await context.UserTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == purpose, cancellationToken);

        if (record is null || record.ConsumedAt is not null || record.ExpiresAt <= DateTime.UtcNow)
        {
            throw new DomainValidationException("That link has expired or was already used. Request a new one.");
        }

        record.ConsumedAt = DateTime.UtcNow;
        return record;
    }
}
