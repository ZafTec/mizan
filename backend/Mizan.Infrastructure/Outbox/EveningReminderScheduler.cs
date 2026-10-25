using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Application.Planning;

namespace Mizan.Infrastructure.Outbox;

/// <summary>
/// Every <see cref="Interval"/>, schedules each person's next evening reminder
/// into the outbox. The outbox keys a job by person, day and time, so a scan
/// that finds the same reminder again adds nothing; the scan only has to be
/// frequent enough to notice a plan or window that changed today. Off with the
/// dispatcher (<c>Outbox:Enabled=false</c>), which is how tests drive it.
/// </summary>
public sealed class EveningReminderScheduler : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopes;
    private readonly OutboxOptions _options;
    private readonly ILogger<EveningReminderScheduler> _logger;

    public EveningReminderScheduler(IServiceScopeFactory scopes, IOptions<OutboxOptions> options, ILogger<EveningReminderScheduler> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;

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
                // A failed scan is retried on the next tick; the reminders it missed are still ahead of it.
                _logger.LogError(ex, "Scheduling evening reminders failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>One scan. Public so a test can drive it without a timer.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<EveningReminders>().ScheduleAsync(cancellationToken);
    }
}
