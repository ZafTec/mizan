using Microsoft.Extensions.Options;

namespace Mizan.Telegram.Bot;

/// <summary>
/// getUpdates in a loop, for development.
///
/// Production uses a webhook, and the difference stops here: both paths hand
/// the same <see cref="Update"/> to the same handler. Long polling needs no
/// public hostname and no TLS, which is the whole reason to keep it.
/// </summary>
public sealed class LongPollWorker : BackgroundService
{
    /// <summary>Telegram holds the request open this long when nothing arrives.</summary>
    private const int PollSeconds = 30;

    private readonly IServiceScopeFactory _scopes;
    private readonly TelegramBotOptions _options;
    private readonly ILogger<LongPollWorker> _logger;

    public LongPollWorker(
        IServiceScopeFactory scopes,
        IOptions<TelegramBotOptions> options,
        ILogger<LongPollWorker> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsConfigured || _options.UseWebhook)
        {
            _logger.LogInformation(
                "Long polling disabled ({Reason})",
                _options.IsConfigured ? "webhook mode" : "no bot token");
            return;
        }

        using (var scope = _scopes.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TelegramClient>()
                .SetCommandsAsync(stoppingToken);
        }

        _logger.LogInformation("Telegram long polling started");

        long offset = 0;
        var backoff = TimeSpan.FromSeconds(1);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var telegram = scope.ServiceProvider.GetRequiredService<TelegramClient>();
                var handler = scope.ServiceProvider.GetRequiredService<UpdateHandler>();

                var updates = await telegram.GetUpdatesAsync(offset, PollSeconds, stoppingToken);
                backoff = TimeSpan.FromSeconds(1);

                foreach (var update in updates)
                {
                    // Acknowledged by advancing the offset before handling, so
                    // a message the handler chokes on cannot wedge the loop
                    // forever - the failure is logged and the queue moves on.
                    offset = update.UpdateId + 1;
                    await handler.HandleAsync(update, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Long poll failed; retrying in {Seconds}s", backoff.TotalSeconds);
                await Task.Delay(backoff, stoppingToken);
                backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 60));
            }
        }
    }
}
