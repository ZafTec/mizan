using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Auth;

/// <summary>
/// Completes a Google or GitHub sign-in. Runs after the OAuth handler has
/// verified the provider's response, so everything here is already trusted.
/// </summary>
public record ExternalLoginCommand(
    string Provider,
    string ProviderKey,
    string Email,
    string? Name,
    string? Image,
    string? IpAddress,
    string? UserAgent) : IRequest<string>;

public class ExternalLoginCommandHandler : IRequestHandler<ExternalLoginCommand, string>
{
    private readonly IMizanDbContext _context;
    private readonly ISessionService _sessions;
    private readonly IUserCacheInvalidator _cache;

    public ExternalLoginCommandHandler(
        IMizanDbContext context,
        ISessionService sessions,
        IUserCacheInvalidator cache)
    {
        _context = context;
        _sessions = sessions;
        _cache = cache;
    }

    public async Task<string> Handle(ExternalLoginCommand request, CancellationToken cancellationToken)
    {
        var email = AuthEmailAddress.Normalize(request.Email);
        var now = DateTime.UtcNow;

        var link = await _context.ExternalLogins
            .FirstOrDefaultAsync(
                l => l.Provider == request.Provider && l.ProviderKey == request.ProviderKey,
                cancellationToken);

        User user;
        if (link is not null)
        {
            user = await _context.Users.FirstAsync(u => u.Id == link.UserId, cancellationToken);
        }
        else
        {
            user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken)
                ?? CreateUser(email, request.Name, request.Image, now);

            // Linking on a matching email is only safe because the provider
            // asserts the address and both providers we accept verify it.
            _context.ExternalLogins.Add(new ExternalLogin
            {
                Id = Guid.CreateVersion7(),
                UserId = user.Id,
                Provider = request.Provider,
                ProviderKey = request.ProviderKey,
                CreatedAt = now,
            });
        }

        if (user.Banned && (user.BanExpires is null || user.BanExpires > now))
        {
            throw new ForbiddenAccessException(user.BanReason ?? "This account is suspended.");
        }

        if (!user.EmailVerified)
        {
            user.EmailVerified = true;
            user.UpdatedAt = now;
        }

        if (user.Image is null && request.Image is not null)
        {
            user.Image = request.Image;
            user.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await _cache.InvalidateAsync(user.Id, cancellationToken);

        return await _sessions.CreateAsync(user.Id, request.IpAddress, request.UserAgent, cancellationToken);
    }

    private User CreateUser(string email, string? name, string? image, DateTime now)
    {
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            Image = image,
            EmailVerified = true,
            Role = "user",
            CreatedAt = now,
            UpdatedAt = now,
        };
        _context.Users.Add(user);
        return user;
    }
}
