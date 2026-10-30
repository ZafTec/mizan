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

    /// <summary>The bot service's own unauthenticated /health. Not configured without the Telegram.BotServiceUrl setting.</summary>
    Task<ProbeResult> TelegramBotAsync(CancellationToken cancellationToken);

    /// <summary>These read the runtime settings, which an administrator can change without a restart.</summary>
    Task<bool> SmtpConfiguredAsync(CancellationToken cancellationToken);
    Task<bool> AiConfiguredAsync(CancellationToken cancellationToken);
    Task<bool> PushConfiguredAsync(CancellationToken cancellationToken);
    bool PricesWorkerConfigured { get; }
    Task<bool> StorageConfiguredAsync(CancellationToken cancellationToken);
}
