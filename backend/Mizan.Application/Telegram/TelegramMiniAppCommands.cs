using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Features;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Telegram;
using Mizan.Domain.Features;

namespace Mizan.Application.Telegram;

/// <summary>
/// Turns a Telegram identity the bot has verified into an ordinary Mizan session.
///
/// Service-key only, like <see cref="ResolveTelegramUserQuery"/>: the Telegram
/// id is only worth something because the bot checked Telegram's signature on
/// it, and the bot is the only party that can. The session it creates is the
/// same row a browser sign-in makes, with a shorter life, so every rule that
/// applies to a signed-in user applies to the Mini App without a second path.
/// Refusals mirror a password sign-in: a banned or locked account cannot open
/// one, and the bot's Pro gate holds here too.
/// </summary>
public record CreateTelegramMiniAppSessionCommand(long TelegramUserId, string? IpAddress, string? UserAgent)
    : IRequest<TelegramMiniAppSessionResponse>;

public class CreateTelegramMiniAppSessionCommandHandler
    : IRequestHandler<CreateTelegramMiniAppSessionCommand, TelegramMiniAppSessionResponse>
{
    private readonly IMizanDbContext _context;
    private readonly ISessionService _sessions;
    private readonly IEntitlementService _entitlements;
    private readonly ITelegramSettings _settings;
    private readonly FeatureGate _features;

    public CreateTelegramMiniAppSessionCommandHandler(
        IMizanDbContext context,
        ISessionService sessions,
        IEntitlementService entitlements,
        ITelegramSettings settings,
        FeatureGate features)
    {
        _context = context;
        _sessions = sessions;
        _entitlements = entitlements;
        _settings = settings;
        _features = features;
    }

    public async Task<TelegramMiniAppSessionResponse> Handle(
        CreateTelegramMiniAppSessionCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.TelegramLinks.AsNoTracking()
            .Where(l => l.TelegramUserId == request.TelegramUserId)
            .Select(l => l.User!)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("This Telegram account is not linked to a Mizan account.");

        // The same doors a password sign-in checks, in the same order, so the
        // Mini App cannot be a way around any of them.
        if (user.LockoutEnd is { } until && until > DateTime.UtcNow)
        {
            throw new AccountLockedException(until);
        }

        if (user.Banned && (user.BanExpires is null || user.BanExpires > DateTime.UtcNow))
        {
            throw new ForbiddenAccessException(user.BanReason ?? "This account is suspended.");
        }

        if (!user.EmailVerified)
        {
            throw new EmailNotVerifiedException();
        }

        if (!(await _entitlements.GetAsync(user.Id, cancellationToken)).IsPro)
        {
            throw new UpgradeRequiredException("The Telegram bot is part of Pro. Upgrade to open Mizan from Telegram.");
        }

        // The request is the service's, not the user's, so the household is the linked user's.
        await _features.EnsureEnabledForUserAsync(FeatureFlags.TelegramBot, user.Id, cancellationToken);

        // Same courtesy as resolving a message: the settings page can say the
        // chat was used today, not only that it was linked once.
        await _context.TelegramLinks
            .Where(l => l.TelegramUserId == request.TelegramUserId)
            .ExecuteUpdateAsync(
                l => l.SetProperty(x => x.LastSeenAt, DateTime.UtcNow),
                cancellationToken);

        var lifetime = (await _settings.GetAsync(cancellationToken)).MiniAppSessionLifetime;
        var token = await _sessions.CreateAsync(user.Id, lifetime, request.IpAddress, request.UserAgent, cancellationToken);

        return new TelegramMiniAppSessionResponse(token, DateTime.UtcNow.Add(lifetime), user.Id);
    }
}
