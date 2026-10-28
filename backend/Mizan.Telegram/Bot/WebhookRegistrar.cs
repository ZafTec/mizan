using Microsoft.Extensions.Options;

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
/// Registers the webhook in the background, retrying with backoff until Telegram accepts it.
///
/// Telegram can refuse <c>setWebhook</c> for reasons that pass on their own, such as a resolver
/// still caching NXDOMAIN for a hostname created minutes ago. A refusal at startup used to leave the
/// bot online but deaf, with only a status code in the log. Now the refusal's description is logged
/// and reported on <c>/health</c>, the call is retried (5 s doubling to 5 min), and startup never
/// waits on it, so the health check answers throughout.
/// </summary>
public sealed class WebhookRegistrar : BackgroundService
{
    public static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(5);

    private readonly TelegramClient _telegram;
    private readonly TelegramBotOptions _options;
    private readonly WebhookState _state;
    private readonly ILogger<WebhookRegistrar> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private int _attempts;

    public WebhookRegistrar(
        TelegramClient telegram,
        IOptions<TelegramBotOptions> options,
        WebhookState state,
        ILogger<WebhookRegistrar> logger,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _telegram = telegram;
        _options = options.Value;
        _state = state;
        _logger = logger;
        _delay = delay ?? Task.Delay;
    }

    public string WebhookUrl => $"{_options.PublicUrl.TrimEnd('/')}/telegram/webhook";

    /// <summary>Whether this process registers a webhook at all.</summary>
    public bool Enabled => _options.IsConfigured && _options.UseWebhook && !string.IsNullOrWhiteSpace(_options.WebhookSecret);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.IsConfigured && _options.UseWebhook && string.IsNullOrWhiteSpace(_options.WebhookSecret))
            _state.Set("failed", "TelegramBot:WebhookSecret is empty, so no webhook is registered.", 0);
        if (!Enabled) return;

        var delay = FirstDelay;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (await TryRegisterAsync(stoppingToken)) return;

            try { await _delay(delay, stoppingToken); }
            catch (OperationCanceledException) { return; }
            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxDelay.Ticks));
        }
    }

    /// <summary>One attempt. True once Telegram has answered <c>ok: true</c>.</summary>
    public async Task<bool> TryRegisterAsync(CancellationToken ct)
    {
        _attempts++;
        if (_attempts == 1) _state.Set("pending", null, 0);

        var result = await _telegram.SetWebhookAsync(WebhookUrl, _options.WebhookSecret!, ct);
        if (!result.Ok)
        {
            _state.Set("failed", result.Description, _attempts);
            _logger.LogWarning(
                "Telegram refused the webhook at {Url} (attempt {Attempt}): {Description}; retrying",
                WebhookUrl, _attempts, result.Description);
            return false;
        }

        _state.Set("registered", null, _attempts);
        _logger.LogInformation("Telegram webhook registered at {Url}", WebhookUrl);

        // Telegram took the URL; say so if it is not the one we sent, or if it cannot deliver to it.
        var info = await _telegram.GetWebhookInfoAsync(ct);
        if (info is not null && !string.Equals(info.Url, WebhookUrl, StringComparison.Ordinal))
            _logger.LogWarning("Telegram reports the webhook as {Actual}, not {Expected}", info.Url, WebhookUrl);
        if (info?.LastErrorMessage is { Length: > 0 } error)
            _logger.LogWarning("Telegram reports a webhook delivery error: {Error}", error);

        return true;
    }
}
