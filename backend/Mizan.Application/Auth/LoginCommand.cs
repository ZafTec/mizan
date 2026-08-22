using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Auth;

public record LoginCommand(string Email, string Password, string? IpAddress, string? UserAgent)
    : IRequest<LoginResult>;

/// <summary>The session token belongs in a cookie; the controller sets it.</summary>
public record LoginResult(string SessionToken, AuthUserDto User);

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(256);
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IMizanDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionService _sessions;
    private readonly IEmailSender _email;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IMizanDbContext context,
        IPasswordHasher passwordHasher,
        ISessionService sessions,
        IEmailSender email,
        ILogger<LoginCommandHandler> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _sessions = sessions;
        _email = email;
        _logger = logger;
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var email = AuthEmailAddress.Normalize(request.Email);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || user.PasswordHash is null)
        {
            throw new InvalidCredentialsException();
        }

        if (user.LockoutEnd is { } until && until > DateTime.UtcNow)
        {
            throw new AccountLockedException(until);
        }

        if (!_passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= MaxFailedAttempts)
            {
                user.LockoutEnd = DateTime.UtcNow.Add(LockoutDuration);
                user.AccessFailedCount = 0;
            }
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            throw new InvalidCredentialsException();
        }

        if (user.Banned && (user.BanExpires is null || user.BanExpires > DateTime.UtcNow))
        {
            throw new ForbiddenAccessException(user.BanReason ?? "This account is suspended.");
        }

        if (!user.EmailVerified)
        {
            throw new EmailNotVerifiedException();
        }

        if (user.AccessFailedCount != 0 || user.LockoutEnd is not null)
        {
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        var token = await _sessions.CreateAsync(user.Id, request.IpAddress, request.UserAgent, cancellationToken);

        await AuthEmailDelivery.TrySendAsync(
            _email,
            AuthEmails.SignInNotification(user.Email, user.Name, request.IpAddress, request.UserAgent),
            _logger,
            user.Id,
            cancellationToken);

        return new LoginResult(token, AuthUserMapper.ToDto(user));
    }
}
