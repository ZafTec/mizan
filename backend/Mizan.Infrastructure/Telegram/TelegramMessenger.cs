using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Outbox;

namespace Mizan.Infrastructure.Telegram;

public sealed class TelegramMessenger : ITelegramMessenger
{
    private readonly IMizanDbContext _context;
    private readonly IOutbox _outbox;
    private readonly IEntitlementService _entitlements;
    private readonly ITelegramBotNotifier _bot;

    public TelegramMessenger(IMizanDbContext context, IOutbox outbox, IEntitlementService entitlements, ITelegramBotNotifier bot)
    {
        _context = context;
        _outbox = outbox;
        _entitlements = entitlements;
        _bot = bot;
    }

    public async Task QueueAsync(Guid userId, string text, string? openPath, string dedupeKey, CancellationToken cancellationToken = default)
    {
        if (!await _bot.IsConfiguredAsync(cancellationToken)) return;
        if (!await _context.TelegramLinks.AnyAsync(l => l.UserId == userId, cancellationToken)) return;
        if (!(await _entitlements.GetAsync(userId, cancellationToken)).IsPro) return;

        await _outbox.EnqueueAsync(
            OutboxJobTypes.TelegramMessage,
            new TelegramMessageJob(userId, text, openPath),
            dedupeKey,
            cancellationToken);
    }
}
