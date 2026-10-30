using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Auth;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Domain.Identity;

namespace Mizan.Application.Admin;

/// <summary>What <c>Mizan.Api --create-admin</c> did. <see cref="PasswordLink"/> is a credential.</summary>
public sealed record AdminBootstrapResult(Guid UserId, bool Created, string PasswordLink);

/// <summary>
/// The first administrator, made from the command line before anyone can sign in to the admin area
/// to configure email (docs/ADMIN.md#first-administrator). The account is verified, holds no
/// password, and gets a one-time link to set one, so no password passes through a shell history.
/// </summary>
public static class AdminBootstrap
{
    /// <summary>Long enough to reach a browser on another machine, short enough not to linger in a terminal scrollback.</summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromHours(24);

    /// <summary>Creates the account, or promotes and verifies an existing one, and issues the link.</summary>
    public static async Task<AdminBootstrapResult> CreateAsync(
        IMizanDbContext context, IAppUrls urls, string email, CancellationToken cancellationToken)
    {
        var normalized = AuthEmailAddress.Normalize(email ?? string.Empty);
        if (!MailAddress.TryCreate(normalized, out var address) || address.Address != normalized)
        {
            throw new ArgumentException("Give the administrator's email address, such as admin@example.com.", nameof(email));
        }

        var now = DateTime.UtcNow;
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
        var created = user is null;
        if (user is null)
        {
            user = new User { Id = Guid.CreateVersion7(), Email = normalized, CreatedAt = now };
            context.Users.Add(user);
        }

        user.Role = "admin";
        user.EmailVerified = true;
        user.Banned = false;
        user.UpdatedAt = now;

        var token = await AuthTokens.IssueAsync(context, user.Id, UserTokenPurpose.PasswordReset, LinkLifetime, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return new AdminBootstrapResult(user.Id, created, urls.ResetPassword(token));
    }
}
