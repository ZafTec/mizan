using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Auth;

public record ForgotPasswordCommand(string Email) : IRequest<Unit>;

public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
    }
}

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Unit>
{
    public static readonly TimeSpan ResetLifetime = TimeSpan.FromHours(1);

    private readonly IMizanDbContext _context;
    private readonly IOutbox _outbox;
    private readonly IAppUrls _urls;

    public ForgotPasswordCommandHandler(
        IMizanDbContext context,
        IOutbox outbox,
        IAppUrls urls)
    {
        _context = context;
        _outbox = outbox;
        _urls = urls;
    }

    public async Task<Unit> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var email = AuthEmailAddress.Normalize(request.Email);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Same answer for a known and an unknown address.
        if (user is null) return Unit.Value;

        var token = await AuthTokens.IssueAsync(
            _context, user.Id, UserTokenPurpose.PasswordReset, ResetLifetime, cancellationToken);

        await AuthEmailDelivery.QueueAsync(
            _outbox,
            AuthEmails.PasswordReset(user.Email, user.Name, _urls.ResetPassword(token)),
            user.Id,
            "reset",
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public record ResetPasswordCommand(string Token, string Password) : IRequest<Unit>;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(10).MaximumLength(256);
    }
}

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionService _sessions;
    private readonly IUserCacheInvalidator _cache;

    public ResetPasswordCommandHandler(
        IMizanDbContext context,
        IPasswordHasher passwordHasher,
        ISessionService sessions,
        IUserCacheInvalidator cache)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _sessions = sessions;
        _cache = cache;
    }

    public async Task<Unit> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var token = await AuthTokens.ConsumeAsync(
            _context, UserTokenPurpose.PasswordReset, request.Token, cancellationToken);

        var user = await _context.Users.FirstAsync(u => u.Id == token.UserId, cancellationToken);
        user.PasswordHash = _passwordHasher.Hash(request.Password);
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        // Reaching the inbox proves the address; an unverified account that
        // resets its password is verified by the same evidence.
        user.EmailVerified = true;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Whoever forced the reset does not get to keep an old session.
        await _sessions.RevokeAllAsync(user.Id, cancellationToken);
        await _cache.InvalidateAsync(user.Id, cancellationToken);
        return Unit.Value;
    }
}

/// <summary>
/// CurrentSessionToken is supplied by the controller from the cookie, never by
/// the client body: it is what keeps this browser signed in while every other
/// one is cut.
/// </summary>
public record ChangePasswordCommand(string? CurrentPassword, string NewPassword, string? CurrentSessionToken = null)
    : IRequest<Unit>;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(10).MaximumLength(256);
    }
}

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionService _sessions;

    public ChangePasswordCommandHandler(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        ISessionService sessions)
    {
        _context = context;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _sessions = sessions;
    }

    public async Task<Unit> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var user = await _context.Users.FirstAsync(u => u.Id == userId, cancellationToken);

        // A social-only account has no password to prove; it is setting one.
        if (user.PasswordHash is not null)
        {
            if (string.IsNullOrEmpty(request.CurrentPassword)
                || !_passwordHasher.Verify(user.PasswordHash, request.CurrentPassword))
            {
                throw new DomainValidationException("Current password is incorrect.");
            }
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        if (string.IsNullOrEmpty(request.CurrentSessionToken))
        {
            await _sessions.RevokeAllAsync(userId, cancellationToken);
        }
        else
        {
            await _sessions.RevokeAllExceptAsync(userId, request.CurrentSessionToken, cancellationToken);
        }

        return Unit.Value;
    }
}
