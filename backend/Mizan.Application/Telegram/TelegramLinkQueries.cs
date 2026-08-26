using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Telegram;

public record TelegramLinkDto(
    bool Linked,
    string? TelegramUsername,
    DateTime? LinkedAt,
    DateTime? LastSeenAt,
    string? BotUsername,
    bool BotConfigured);

/// <summary>The signed-in user's own link, for the settings page.</summary>
public record GetTelegramLinkQuery : IRequest<TelegramLinkDto>;

public class GetTelegramLinkQueryHandler : IRequestHandler<GetTelegramLinkQuery, TelegramLinkDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ITelegramSettings _settings;

    public GetTelegramLinkQueryHandler(
        IMizanDbContext context, ICurrentUserService currentUser, ITelegramSettings settings)
    {
        _context = context;
        _currentUser = currentUser;
        _settings = settings;
    }

    public async Task<TelegramLinkDto> Handle(GetTelegramLinkQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var link = await _context.TelegramLinks.AsNoTracking()
            .Where(l => l.UserId == userId)
            .Select(l => new { l.TelegramUsername, l.LinkedAt, l.LastSeenAt })
            .FirstOrDefaultAsync(cancellationToken);

        return new TelegramLinkDto(
            link is not null,
            link?.TelegramUsername,
            link?.LinkedAt,
            link?.LastSeenAt,
            _settings.BotUsername,
            _settings.IsConfigured);
    }
}

/// <summary>
/// Who a Telegram chat is, if anyone. The bot's first call on every message.
///
/// Service-key only, and it answers with a user id or nothing - there is no
/// search, no listing, and no way to go the other direction. A bot handed a
/// chat id that was never linked learns exactly that.
/// </summary>
public record ResolveTelegramUserQuery(long TelegramUserId) : IRequest<ResolvedTelegramUser?>;

public record ResolvedTelegramUser(Guid UserId, string? Name, DateTime LinkedAt);

public class ResolveTelegramUserQueryHandler
    : IRequestHandler<ResolveTelegramUserQuery, ResolvedTelegramUser?>
{
    private readonly IMizanDbContext _context;

    public ResolveTelegramUserQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<ResolvedTelegramUser?> Handle(
        ResolveTelegramUserQuery request, CancellationToken cancellationToken)
    {
        var link = await _context.TelegramLinks.AsNoTracking()
            .Where(l => l.TelegramUserId == request.TelegramUserId)
            .Select(l => new { l.UserId, Name = l.User!.Name, l.LinkedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (link is null) return null;

        // Touched on resolve rather than only on link, so settings can answer
        // "is this still connected" with something better than the day it was
        // set up. One indexed single-row update per message.
        await _context.TelegramLinks
            .Where(l => l.TelegramUserId == request.TelegramUserId)
            .ExecuteUpdateAsync(
                l => l.SetProperty(x => x.LastSeenAt, DateTime.UtcNow),
                cancellationToken);

        return new ResolvedTelegramUser(link.UserId, link.Name, link.LinkedAt);
    }
}
