using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Domain.Identity;

namespace Mizan.Application.Auth;

public record VerifyEmailCommand(string Token) : IRequest<Unit>;

public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly IUserCacheInvalidator _cache;

    public VerifyEmailCommandHandler(IMizanDbContext context, IUserCacheInvalidator cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<Unit> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var token = await AuthTokens.ConsumeAsync(
            _context, UserTokenPurpose.EmailVerification, request.Token, cancellationToken);

        var user = await _context.Users.FirstAsync(u => u.Id == token.UserId, cancellationToken);
        user.EmailVerified = true;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await _cache.InvalidateAsync(user.Id, cancellationToken);
        return Unit.Value;
    }
}

public record ResendVerificationCommand(string Email) : IRequest<Unit>;

public class ResendVerificationCommandValidator : AbstractValidator<ResendVerificationCommand>
{
    public ResendVerificationCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
    }
}

public class ResendVerificationCommandHandler : IRequestHandler<ResendVerificationCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly IEmailSender _email;
    private readonly IAppUrls _urls;
    private readonly ILogger<ResendVerificationCommandHandler> _logger;

    public ResendVerificationCommandHandler(
        IMizanDbContext context,
        IEmailSender email,
        IAppUrls urls,
        ILogger<ResendVerificationCommandHandler> logger)
    {
        _context = context;
        _email = email;
        _urls = urls;
        _logger = logger;
    }

    public async Task<Unit> Handle(ResendVerificationCommand request, CancellationToken cancellationToken)
    {
        var email = AuthEmailAddress.Normalize(request.Email);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Says nothing about whether the address exists. The caller gets the
        // same answer either way.
        if (user is null || user.EmailVerified) return Unit.Value;

        var token = await AuthTokens.IssueAsync(
            _context,
            user.Id,
            UserTokenPurpose.EmailVerification,
            RegisterCommandHandler.VerificationLifetime,
            cancellationToken);

        await AuthEmailDelivery.TrySendAsync(
            _email,
            AuthEmails.Verification(user.Email, user.Name, _urls.VerifyEmail(token)),
            _logger,
            user.Id,
            cancellationToken);

        return Unit.Value;
    }
}
