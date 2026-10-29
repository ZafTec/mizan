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

    public async Task QueueAsync(Guid userId, TelegramOutgoing message, string dedupeKey, DateTime? runAfter = null, CancellationToken cancellationToken = default)
    {
        if (!await _bot.IsConfiguredAsync(cancellationToken)) return;
        if (!await _context.TelegramLinks.AnyAsync(l => l.UserId == userId, cancellationToken)) return;
        if (!(await _entitlements.GetAsync(userId, cancellationToken)).IsPro) return;

        var job = new TelegramMessageJob(userId, message.Text, message.OpenPath, message.Card);
        if (runAfter is { } at)
            await _outbox.ScheduleAsync(OutboxJobTypes.TelegramMessage, job, at, dedupeKey, cancellationToken);
        else
            await _outbox.EnqueueAsync(OutboxJobTypes.TelegramMessage, job, dedupeKey, cancellationToken);
    }
}
