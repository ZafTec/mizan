using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly IEmailSender _email;
    private readonly IAppUrls _urls;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IMizanDbContext context,
        IPasswordHasher passwordHasher,
        IEmailSender email,
        IAppUrls urls,
        ILogger<RegisterCommandHandler> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _email = email;
        _urls = urls;
        _logger = logger;
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

        await _context.SaveChangesAsync(cancellationToken);

        await AuthEmailDelivery.TrySendAsync(
            _email,
            AuthEmails.Verification(user.Email, user.Name, _urls.VerifyEmail(token)),
            _logger,
            user.Id,
            cancellationToken);

        return Unit.Value;
    }
}
