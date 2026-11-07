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
    private readonly BotSettings _settings;
    private readonly ILogger<LongPollWorker> _logger;

    public LongPollWorker(IServiceScopeFactory scopes, BotSettings settings, ILogger<LongPollWorker> logger)
    {
        _scopes = scopes;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Settings change while the bot runs, so the mode is decided per round: a change ends the
            // current round, and the next one polls, or waits, with the new settings.
            var changed = _settings.WaitForChangeAsync(stoppingToken);
            var options = _settings.Current;

            try
            {
                if (!options.IsConfigured || options.UseWebhook)
                {
                    if (_settings.Loaded)
                    {
                        _logger.LogInformation(
                            "Long polling off ({Reason})", options.IsConfigured ? "webhook mode" : "no bot token");
                    }

                    await changed;
                    continue;
                }

                using var round = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var polling = PollAsync(round.Token);
                if (await Task.WhenAny(polling, changed) == changed) await round.CancelAsync();
                await polling;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task PollAsync(CancellationToken ct)
    {
        _logger.LogInformation("Telegram long polling started");

        long offset = 0;
        var backoff = TimeSpan.FromSeconds(1);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var telegram = scope.ServiceProvider.GetRequiredService<TelegramClient>();
                var handler = scope.ServiceProvider.GetRequiredService<UpdateHandler>();

                var updates = await telegram.GetUpdatesAsync(offset, PollSeconds, ct);
                backoff = TimeSpan.FromSeconds(1);

                foreach (var update in updates)
                {
                    // Acknowledged by advancing the offset before handling, so
                    // a message the handler chokes on cannot wedge the loop
                    // forever - the failure is logged and the queue moves on.
                    offset = update.UpdateId + 1;
                    await handler.HandleAsync(update, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Long poll failed; retrying in {Seconds}s", backoff.TotalSeconds);
                try { await Task.Delay(backoff, ct); }
                catch (OperationCanceledException) { return; }
                backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 60));
            }
        }
    }
}
