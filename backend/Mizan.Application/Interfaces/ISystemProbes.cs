namespace Mizan.Application.Interfaces;

/// <summary>How a timed check went: null <see cref="Elapsed"/> means it failed, and <see cref="Error"/> says how.</summary>
public sealed record ProbeResult(bool Configured, TimeSpan? Elapsed, string? Error);

/// <summary>
/// The parts of the system health page that need configuration or the network:
/// whether each service is set up, and a round trip to the ones that can
/// answer. The query turns these into statuses with the Domain rules.
/// </summary>
public interface ISystemProbes
{
    Task<ProbeResult> DatabaseAsync(CancellationToken cancellationToken);
    Task<ProbeResult> RedisAsync(CancellationToken cancellationToken);

    /// <summary>The bot service's own unauthenticated /health. Not configured without Telegram:BotServiceUrl.</summary>
    Task<ProbeResult> TelegramBotAsync(CancellationToken cancellationToken);

    bool SmtpConfigured { get; }
    bool AiConfigured { get; }
    bool PushConfigured { get; }
    bool PricesWorkerConfigured { get; }
    bool StorageConfigured { get; }
}
