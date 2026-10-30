using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Mizan.Application.Notifications;

namespace Mizan.Infrastructure.Outbox;

/// <summary>
/// Every <see cref="Interval"/>, runs <see cref="NotificationScheduler"/>: each enabled schedule's
/// occurrences within its lookahead go into the outbox, among them each person's next evening
/// reminder. Every job is keyed by its occurrence, so a scan that finds the same one again adds
/// nothing; the scan only has to be frequent enough to notice a plan or window that changed today.
/// Off with the dispatcher (<c>Outbox:Enabled=false</c>), which is how tests drive it.
/// </summary>
public sealed class NotificationSchedulerService : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopes;
    private readonly bool _enabled;
    private readonly ILogger<NotificationSchedulerService> _logger;

    public NotificationSchedulerService(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<NotificationSchedulerService> logger)
    {
        _scopes = scopes;
        _enabled = OutboxOptions.IsEnabled(configuration);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled) return;

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // A failed scan is retried on the next tick; what it missed is still within the lookahead.
                _logger.LogError(ex, "Scheduling notifications failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>One scan. Public so a test can drive it without a timer.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<NotificationScheduler>().RunAsync(cancellationToken);
    }
}
