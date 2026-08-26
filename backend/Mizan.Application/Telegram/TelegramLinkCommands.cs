using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Auth;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Telegram;

public record TelegramLinkCodeDto(string Code, string DeepLink, DateTime ExpiresAt);

/// <summary>
/// Issues the code behind the t.me link.
///
/// Five minutes, single use, and issuing a new one kills the old - the user is
/// holding both devices, so there is no reason for a link code to outlive the
/// walk from the browser to the phone. Same one-time token machinery as a
/// password reset, so it is stored as a hash and consumed rather than deleted.
/// </summary>
public record IssueTelegramLinkCodeCommand : IRequest<TelegramLinkCodeDto>;

public class IssueTelegramLinkCodeCommandHandler
    : IRequestHandler<IssueTelegramLinkCodeCommand, TelegramLinkCodeDto>
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ITelegramSettings _settings;

    public IssueTelegramLinkCodeCommandHandler(
        IMizanDbContext context, ICurrentUserService currentUser, ITelegramSettings settings)
    {
        _context = context;
        _currentUser = currentUser;
        _settings = settings;
    }

    public async Task<TelegramLinkCodeDto> Handle(
        IssueTelegramLinkCodeCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        if (!_settings.IsConfigured)
        {
            throw new DomainValidationException("Telegram is not set up on this server.");
        }

        var code = await AuthTokens.IssueAsync(
            _context, userId, UserTokenPurpose.TelegramLink, Lifetime, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new TelegramLinkCodeDto(
            code,
            $"https://t.me/{_settings.BotUsername}?start={code}",
            DateTime.UtcNow.Add(Lifetime));
    }
}

/// <summary>
/// Binds a Telegram account to a Mizan one by spending a code.
///
/// Service-key only: the bot is the only caller, because it is the only party
/// that knows the real Telegram id. Passing an id here is not a claim about
/// who the *user* is - the code is what carries that, and it came from a
/// signed-in browser session.
/// </summary>
public record ConsumeTelegramLinkCommand(string Code, long TelegramUserId, string? TelegramUsername)
    : IRequest<TelegramLinkResult>;

public record TelegramLinkResult(Guid UserId, string? Name);

public class ConsumeTelegramLinkCommandHandler
    : IRequestHandler<ConsumeTelegramLinkCommand, TelegramLinkResult>
{
    private readonly IMizanDbContext _context;

    public ConsumeTelegramLinkCommandHandler(IMizanDbContext context) => _context = context;

    public async Task<TelegramLinkResult> Handle(
        ConsumeTelegramLinkCommand request, CancellationToken cancellationToken)
    {
        var token = await AuthTokens.ConsumeAsync(
            _context, UserTokenPurpose.TelegramLink, request.Code, cancellationToken);

        // Both sides are unique, so re-linking has to replace rather than
        // insert: a user who reinstalls Telegram gets a new id, and a phone
        // handed to someone else must not stay pointed at the old account.
        await _context.TelegramLinks
            .Where(l => l.UserId == token.UserId || l.TelegramUserId == request.TelegramUserId)
            .ExecuteDeleteAsync(cancellationToken);

        _context.TelegramLinks.Add(new TelegramLink
        {
            Id = Guid.CreateVersion7(),
            UserId = token.UserId,
            TelegramUserId = request.TelegramUserId,
            TelegramUsername = Trim(request.TelegramUsername),
            LinkedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
        });

        await _context.SaveChangesAsync(cancellationToken);

        var name = await _context.Users.AsNoTracking()
            .Where(u => u.Id == token.UserId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return new TelegramLinkResult(token.UserId, name);
    }

    private static string? Trim(string? username) =>
        string.IsNullOrWhiteSpace(username) ? null
        : username.TrimStart('@') is { Length: > 64 } tooLong ? tooLong[..64]
        : username.TrimStart('@');
}

/// <summary>
/// Breaks the link. Reachable from the web as the signed-in user, and from the
/// bot for the chat it is currently handling - unlinking has to work from
/// whichever device you still have.
/// </summary>
public record UnlinkTelegramCommand(long? TelegramUserId = null) : IRequest<bool>;

public class UnlinkTelegramCommandHandler : IRequestHandler<UnlinkTelegramCommand, bool>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UnlinkTelegramCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(UnlinkTelegramCommand request, CancellationToken cancellationToken)
    {
        var query = _context.TelegramLinks.AsQueryable();

        if (request.TelegramUserId is { } telegramUserId)
        {
            query = query.Where(l => l.TelegramUserId == telegramUserId);
        }
        else
        {
            var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
            query = query.Where(l => l.UserId == userId);
        }

        return await query.ExecuteDeleteAsync(cancellationToken) > 0;
    }
}
