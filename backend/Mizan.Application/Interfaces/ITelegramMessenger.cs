namespace Mizan.Application.Interfaces;

/// <summary>
/// Sends a line of text to a user's linked Telegram chat, through the bot
/// service. The API never holds the bot token, so this queues an outbox job
/// that asks the bot to send it (docs/TELEGRAM.md#evening-reminder).
/// </summary>
public interface ITelegramMessenger
{
    /// <summary>
    /// Stages the message with the caller's unit of work. Nothing is queued when
    /// the bot service is not configured, the account has no Telegram link, or
    /// the account is not Pro (the bot is a Pro feature).
    /// </summary>
    /// <param name="openPath">A Mini App path such as <c>plan?planId=...</c> for an Open button, or null.</param>
    Task QueueAsync(Guid userId, string text, string? openPath, string dedupeKey, CancellationToken cancellationToken = default);
}
