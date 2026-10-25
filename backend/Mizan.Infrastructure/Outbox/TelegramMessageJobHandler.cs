using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Telegram;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Telegram;

namespace Mizan.Infrastructure.Outbox;

public sealed record TelegramMessageJob(Guid UserId, string Text, string? OpenPath);

/// <summary>
/// Hands one message to the bot service for the user's linked chat. The link is
/// read again when the job runs: a chat unlinked in the meantime gets nothing.
/// </summary>
public sealed class TelegramMessageJobHandler : IOutboxHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IMizanDbContext _context;
    private readonly ITelegramBotNotifier _bot;

    public TelegramMessageJobHandler(IMizanDbContext context, ITelegramBotNotifier bot)
    {
        _context = context;
        _bot = bot;
    }

    public string Type => OutboxJobTypes.TelegramMessage;

    public int Concurrency => 4;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        TelegramMessageJob job;
        try { job = JsonSerializer.Deserialize<TelegramMessageJob>(payload, Json) ?? throw new OutboxPermanentException("The queued Telegram message was empty."); }
        catch (JsonException ex) { throw new OutboxPermanentException("The queued Telegram message could not be read.", ex); }

        if (!await _bot.IsConfiguredAsync(cancellationToken)) return;

        var telegramUserId = await _context.TelegramLinks.AsNoTracking()
            .Where(l => l.UserId == job.UserId)
            .Select(l => (long?)l.TelegramUserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (telegramUserId is not { } chat) return;

        switch (await _bot.SendAsync(new TelegramNotifyRequest(chat, job.Text, job.OpenPath), cancellationToken))
        {
            case TelegramNotifyOutcome.Sent:
                return;
            case TelegramNotifyOutcome.Refused:
                throw new OutboxPermanentException("The bot service refused the message.");
            default:
                throw new InvalidOperationException("The bot service did not deliver the message.");
        }
    }
}
