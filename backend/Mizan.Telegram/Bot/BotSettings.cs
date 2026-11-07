using Microsoft.Extensions.Options;
using Mizan.Contracts.Telegram;
using Mizan.Telegram.Backend;

namespace Mizan.Telegram.Bot;

/// <summary>
/// The bot's current settings: its environment plus the runtime settings the API last answered with
/// (docs/TELEGRAM.md#configure-and-run). Until the first answer the bot has no token and stays idle.
/// Everything that needs a setting reads <see cref="Current"/> when it acts, through
/// <c>IOptions&lt;TelegramBotOptions&gt;</c>, so a change applies without a restart.
/// </summary>
public sealed class BotSettings
{
    private readonly TelegramBotEnvironment _environment;
    private readonly object _gate = new();
    private TelegramBotSettings? _remote;
    private TelegramBotOptions _current;
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public BotSettings(IOptions<TelegramBotEnvironment> environment)
    {
        _environment = environment.Value;
        _current = _environment.With(null);
    }

    public TelegramBotOptions Current
    {
        get { lock (_gate) return _current; }
    }

    /// <summary>Whether the API has answered at least once.</summary>
    public bool Loaded
    {
        get { lock (_gate) return _remote is not null; }
    }

    /// <summary>Completes the next time <see cref="Apply"/> changes something.</summary>
    public Task WaitForChangeAsync(CancellationToken ct)
    {
        Task changed;
        lock (_gate) changed = _changed.Task;
        return changed.WaitAsync(ct);
    }

    /// <summary>Takes the API's answer. True when it differs from the last one.</summary>
    public bool Apply(TelegramBotSettings remote)
    {
        TaskCompletionSource signal;
        lock (_gate)
        {
            if (remote == _remote) return false;
            _remote = remote;
            _current = _environment.With(remote);
            signal = _changed;
            _changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        signal.TrySetResult();
        return true;
    }
}

/// <summary>
/// Reads the bot's settings from the API at startup (retrying until the API answers), on the API's
/// <c>POST /internal/settings-changed</c>, and every few minutes in case a signal was missed. When they
/// change it brings Telegram in line: registers the webhook with the new secret, or removes it for long
/// polling, and sets the command list and menu button.
/// </summary>
public sealed class BotSettingsRefresher : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly BotSettings _settings;
    private readonly IServiceScopeFactory _scopes;
    private readonly WebhookRegistrar _registrar;
    private readonly WebhookState _state;
    private readonly ILogger<BotSettingsRefresher> _logger;
    private readonly SemaphoreSlim _refreshing = new(1, 1);
    private readonly object _gate = new();
    private CancellationTokenSource? _reconcile;
    private CancellationToken _stopping = CancellationToken.None;

    public BotSettingsRefresher(
        BotSettings settings,
        IServiceScopeFactory scopes,
        WebhookRegistrar registrar,
        WebhookState state,
        ILogger<BotSettingsRefresher> logger)
    {
        _settings = settings;
        _scopes = scopes;
        _registrar = registrar;
        _state = state;
        _logger = logger;
    }

    /// <summary>The task bringing Telegram in line with the last change, for tests and <c>/health</c>.</summary>
    public Task Reconciled { get; private set; } = Task.CompletedTask;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stopping = stoppingToken;
        var retry = TimeSpan.FromSeconds(1);

        while (!stoppingToken.IsCancellationRequested)
        {
            var read = await RefreshAsync(stoppingToken);
            var wait = read ? Interval : retry;
            retry = read ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(Math.Min(retry.TotalSeconds * 2, 60));

            try { await Task.Delay(wait, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>Reads the settings once. False when the API did not answer.</summary>
    public async Task<bool> RefreshAsync(CancellationToken ct)
    {
        await _refreshing.WaitAsync(ct);
        try
        {
            TelegramBotSettings? remote;
            using (var scope = _scopes.CreateScope())
            {
                remote = await scope.ServiceProvider.GetRequiredService<MizanApiClient>().GetBotSettingsAsync(ct);
            }

            if (remote is null) return false;
            if (_settings.Apply(remote)) StartReconcile();
            return true;
        }
        finally
        {
            _refreshing.Release();
        }
    }

    private void StartReconcile()
    {
        lock (_gate)
        {
            _reconcile?.Cancel();
            _reconcile?.Dispose();
            _reconcile = CancellationTokenSource.CreateLinkedTokenSource(_stopping);
            var token = _reconcile.Token;
            var options = _settings.Current;
            Reconciled = Task.Run(() => ReconcileAsync(options, token), CancellationToken.None);
        }
    }

    private async Task ReconcileAsync(TelegramBotOptions options, CancellationToken ct)
    {
        try
        {
            if (!options.IsConfigured)
            {
                _state.Set("disabled", null, 0);
                _logger.LogWarning("Telegram bot is not configured; the service is running but idle");
                return;
            }

            using var scope = _scopes.CreateScope();
            var telegram = scope.ServiceProvider.GetRequiredService<TelegramClient>();

            // Telegram, not LongPollWorker, decides which mode is live: it will not deliver getUpdates while a
            // webhook is registered, so switching modes has to touch Telegram's side too.
            if (options.UseWebhook && string.IsNullOrWhiteSpace(options.WebhookSecret))
            {
                // Every update would 404 with no secret to check against, so registering would point Telegram at a dead end.
                _state.Set("failed", "Telegram.WebhookSecret is empty, so no webhook is registered.", 0);
                _logger.LogError("Telegram.UseWebhook is on but Telegram.WebhookSecret is empty; not registering a webhook");
            }
            else if (!options.UseWebhook)
            {
                _state.Set("disabled", null, 0);
                await telegram.DeleteWebhookAsync(ct);
            }

            await BotProfile.ApplyAsync(telegram, options, ct);

            if (options.UseWebhook && !string.IsNullOrWhiteSpace(options.WebhookSecret))
            {
                await _registrar.RegisterAsync(telegram, options, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // A newer change took over, or the service is stopping.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Applying the Telegram settings failed; the next change or refresh tries again");
        }
    }

    public override void Dispose()
    {
        lock (_gate) _reconcile?.Dispose();
        _refreshing.Dispose();
        base.Dispose();
    }
}
