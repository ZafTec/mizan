namespace Mizan.Telegram.Bot;

/// <summary>Where webhook registration stands, for <c>/health</c>.</summary>
public sealed class WebhookState
{
    private readonly object _gate = new();
    private WebhookStatus _status = new("disabled", null, 0);

    public WebhookStatus Current
    {
        get { lock (_gate) return _status; }
    }

    internal void Set(string state, string? lastError, int attempts)
    {
        lock (_gate) _status = new WebhookStatus(state, lastError, attempts);
    }
}

/// <summary><c>disabled</c> (long polling or not configured), <c>pending</c>, <c>registered</c> or <c>failed</c>.</summary>
public sealed record WebhookStatus(string State, string? LastError, int Attempts);


/// <summary>
/// Registers the webhook, retrying with backoff until Telegram accepts it.
///
/// Telegram can refuse <c>setWebhook</c> for reasons that pass on their own, such as a resolver
/// still caching NXDOMAIN for a hostname created minutes ago. The refusal's description is logged
/// and reported on <c>/health</c>, the call is retried (5 s doubling to 5 min), and nothing waits on
/// it, so the health check answers throughout. <see cref="BotSettingsRefresher"/> starts it again,
/// cancelling the previous run, whenever the settings change, such as a rotated secret.
/// </summary>
public sealed class WebhookRegistrar
{
    public static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(5);

    private readonly WebhookState _state;
    private readonly ILogger<WebhookRegistrar> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public WebhookRegistrar(WebhookState state, ILogger<WebhookRegistrar> logger)
        : this(state, logger, null)
    {
    }

    public WebhookRegistrar(WebhookState state, ILogger<WebhookRegistrar> logger, Func<TimeSpan, CancellationToken, Task>? delay)
    {
        _state = state;
        _logger = logger;
        _delay = delay ?? Task.Delay;
    }

    public static string WebhookUrl(TelegramBotOptions options) => $"{options.PublicUrl.TrimEnd('/')}/telegram/webhook";

    /// <summary>Tries until Telegram accepts the webhook or <paramref name="ct"/> is cancelled.</summary>
    public async Task RegisterAsync(TelegramClient telegram, TelegramBotOptions options, CancellationToken ct)
    {
        var delay = FirstDelay;
        var attempt = 0;
        while (!ct.IsCancellationRequested)
        {
            if (await TryRegisterAsync(telegram, options, ++attempt, ct)) return;

            await _delay(delay, ct);
            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxDelay.Ticks));
        }
    }

    /// <summary>One attempt. True once Telegram has answered <c>ok: true</c>.</summary>
    public async Task<bool> TryRegisterAsync(TelegramClient telegram, TelegramBotOptions options, int attempt, CancellationToken ct)
    {
        if (attempt == 1) _state.Set("pending", null, 0);

        var url = WebhookUrl(options);
        var result = await telegram.SetWebhookAsync(url, options.WebhookSecret!, ct);
        if (!result.Ok)
        {
            _state.Set("failed", result.Description, attempt);
            _logger.LogWarning(
                "Telegram refused the webhook at {Url} (attempt {Attempt}): {Description}; retrying",
                url, attempt, result.Description);
            return false;
        }

        _state.Set("registered", null, attempt);
        _logger.LogInformation("Telegram webhook registered at {Url}", url);

        // Telegram took the URL; say so if it is not the one we sent, or if it cannot deliver to it.
        var info = await telegram.GetWebhookInfoAsync(ct);
        if (info is not null && !string.Equals(info.Url, url, StringComparison.Ordinal))
            _logger.LogWarning("Telegram reports the webhook as {Actual}, not {Expected}", info.Url, url);
        if (info?.LastErrorMessage is { Length: > 0 } error)
            _logger.LogWarning("Telegram reports a webhook delivery error: {Error}", error);

        return true;
    }
}
