using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Domain.Identity;
using Mizan.Domain.Streaks;

namespace Mizan.Application.Auth;

/// <summary>
/// <paramref name="TimeZoneId"/> comes from the browser, so it is a hint rather
/// than a claim: an unrecognised value is dropped and the user is treated as
/// UTC until they set one in settings.
/// </summary>
public record RegisterCommand(string Email, string Password, string? Name, string? TimeZoneId = null)
    : IRequest<Unit>;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        // Length is the only password rule worth enforcing. The dropped
        // haveIBeenPwned check bought a network call on the signup path.
        RuleFor(x => x.Password).NotEmpty().MinimumLength(10).MaximumLength(256);
        RuleFor(x => x.Name).MaximumLength(255);
    }
}

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Unit>
{
    public static readonly TimeSpan VerificationLifetime = TimeSpan.FromHours(24);

    private readonly IMizanDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOutbox _outbox;
    private readonly IAppUrls _urls;

    public RegisterCommandHandler(
        IMizanDbContext context,
        IPasswordHasher passwordHasher,
        IOutbox outbox,
        IAppUrls urls)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _outbox = outbox;
        _urls = urls;
    }

    public async Task<Unit> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var email = AuthEmailAddress.Normalize(request.Email);

        if (await _context.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new DomainValidationException("An account with that email already exists.");
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim(),
            EmailVerified = false,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = "user",
            // Getting this at signup is what stops a new user's first week of
            // streaks being computed against the wrong midnight.
            TimeZoneId = StreakClock.IsKnownZone(request.TimeZoneId) ? request.TimeZoneId : null,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _context.Users.Add(user);

        var token = SecureToken.Generate();
        _context.UserTokens.Add(new UserToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Purpose = UserTokenPurpose.EmailVerification,
            TokenHash = SecureToken.Hash(token),
            CreatedAt = now,
            ExpiresAt = now.Add(VerificationLifetime),
        });

        await AuthEmailDelivery.QueueAsync(
            _outbox,
            AuthEmails.Verification(user.Email, user.Name, _urls.VerifyEmail(token)),
            user.Id,
            "verify",
            cancellationToken);

        // One save: the user, the token and the queued mail commit together.
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
