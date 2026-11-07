using Mizan.Contracts.Prices;
using Mizan.Contracts.Telegram;

namespace Mizan.Application.Interfaces;

/// <summary>
/// The runtime settings the bot service and the prices worker need. They hold no database and no key
/// ring, so the API hands them over the internal network to the service key alone
/// (docs/ADMIN.md#runtime-settings).
/// </summary>
public interface IServiceSettings
{
    Task<TelegramBotSettings> GetTelegramBotAsync(CancellationToken cancellationToken = default);

    Task<PriceCrawlSettings> GetPriceCrawlAsync(CancellationToken cancellationToken = default);
}
