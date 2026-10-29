using Mizan.Contracts.Telegram;

namespace Mizan.Application.Interfaces;

/// <summary>What a Telegram message says: the plain-text form, an optional Mini App page, and the structured card.</summary>
public sealed record TelegramOutgoing(string Text, string? OpenPath = null, TelegramNotifyCard? Card = null);

/// <summary>
/// Sends a message to a user's linked Telegram chat, through the bot
/// service. The API never holds the bot token, so this queues an outbox job
/// that asks the bot to send it (docs/TELEGRAM.md#notifications-in-telegram).
/// </summary>
public interface ITelegramMessenger
{
    /// <summary>
    /// Stages the message with the caller's unit of work, to go out at <paramref name="runAfter"/>
    /// (now when null). Nothing is queued when the bot service is not configured, the account has no
    /// Telegram link, or the account is not Pro (the bot is a Pro feature).
    /// </summary>
    Task QueueAsync(Guid userId, TelegramOutgoing message, string dedupeKey, DateTime? runAfter = null, CancellationToken cancellationToken = default);
}
