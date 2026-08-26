using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;

namespace Mizan.Infrastructure.Outbox;

/// <summary>
/// Claims jobs and runs them. One loop, one table, per-type concurrency.
///
/// Two separate workers would mean two polling loops, two retry policies and
/// two things to watch. But a single sequential loop has a real problem: an
/// eval run is minutes of provider calls, and it would sit in front of
/// somebody's password reset. So the loop claims up to each type's cap and
/// runs them together.
///
/// Claiming uses FOR UPDATE SKIP LOCKED, so two API containers can share the
/// table without handing the same job to both. There is one container today;
/// this costs nothing and removes the question.
/// </summary>
public class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        IServiceScopeFactory scopes,
        IOptions<OutboxOptions> options,
        ILogger<OutboxDispatcher> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Outbox dispatcher disabled by configuration");
            return;
        }

        // Nothing in the queue is urgent enough to race application startup.
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var ran = await RunOnceAsync(stoppingToken);

                // Only idle when there was nothing to do. A backlog drains as
                // fast as the handlers allow rather than at one batch per poll.
                if (!ran) await Task.Delay(TimeSpan.FromSeconds(_options.PollSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // The loop itself failing - a dropped connection, say - must
                // not take the dispatcher down for the life of the process.
                _logger.LogError(ex, "Outbox dispatcher loop failed; retrying");
                await Task.Delay(TimeSpan.FromSeconds(_options.PollSeconds), stoppingToken);
            }
        }
    }

    /// <summary>One pass. Public so a test can drive it without a timer.</summary>
    public async Task<bool> RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IOutboxHandler>().ToDictionary(h => h.Type);

        if (handlers.Count == 0) return false;

        await ReleaseStaleAsync(scope.ServiceProvider, cancellationToken);

        var work = new List<Task>();
        var claimedAny = false;

        foreach (var handler in handlers.Values)
        {
            var claimed = await ClaimAsync(
                scope.ServiceProvider, handler.Type, handler.Concurrency, cancellationToken);

            foreach (var jobId in claimed)
            {
                claimedAny = true;
                work.Add(RunJobAsync(jobId, handler.Type, cancellationToken));
            }
        }

        if (work.Count > 0) await Task.WhenAll(work);
        return claimedAny;
    }

    /// <summary>
    /// Marks up to <paramref name="take"/> jobs Running and returns their ids.
    /// SKIP LOCKED is what makes this safe with more than one worker: a row
    /// another transaction has claimed is passed over rather than waited on.
    /// </summary>
    private static async Task<List<Guid>> ClaimAsync(
        IServiceProvider provider, string type, int take, CancellationToken cancellationToken)
    {
        var db = provider.GetRequiredService<MizanDbContext>();

        return await db.Database.SqlQueryRaw<Guid>(
            """
            UPDATE outbox_jobs SET status = 1, started_at = NOW(), attempts = attempts + 1
            WHERE id IN (
                SELECT id FROM outbox_jobs
                WHERE type = {0} AND status IN (0, 3) AND run_after <= NOW()
                ORDER BY run_after, created_at
                LIMIT {1}
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id AS "Value";
            """,
            type,
            take).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// A worker that died mid-job leaves the row Running forever. Nothing else
    /// will ever pick it up, so the job silently never happens - which for a
    /// password reset is the failure mode this whole table exists to remove.
    /// </summary>
    private async Task ReleaseStaleAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        var db = provider.GetRequiredService<MizanDbContext>();
        var cutoff = DateTime.UtcNow.AddMinutes(-_options.StaleAfterMinutes);

        var released = await db.OutboxJobs
            .Where(j => j.Status == OutboxJobStatus.Running && j.StartedAt < cutoff)
            .ExecuteUpdateAsync(
                j => j.SetProperty(x => x.Status, OutboxJobStatus.Failed)
                    .SetProperty(x => x.LastError, "Worker stopped before the job finished")
                    .SetProperty(x => x.RunAfter, DateTime.UtcNow),
                cancellationToken);

        if (released > 0)
        {
            _logger.LogWarning("Returned {Count} stale outbox jobs to the queue", released);
        }
    }

    private async Task RunJobAsync(Guid jobId, string type, CancellationToken cancellationToken)
    {
        // A scope per job: handlers get their own DbContext, and one job's
        // change tracker cannot leak into another's.
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var handler = scope.ServiceProvider.GetServices<IOutboxHandler>().First(h => h.Type == type);

        var job = await db.OutboxJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null) return;

        try
        {
            await handler.HandleAsync(job.Payload, cancellationToken);

            job.Status = OutboxJobStatus.Succeeded;
            job.LastError = null;
            job.CompletedAt = DateTime.UtcNow;
        }
        catch (OutboxPermanentException ex)
        {
            // Retrying cannot fix it, so do not spend four more attempts
            // proving that.
            _logger.LogError(ex, "Outbox job {JobId} ({Type}) failed permanently", jobId, type);
            job.Status = OutboxJobStatus.DeadLettered;
            job.LastError = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            var exhausted = job.Attempts >= _options.MaxAttempts;

            job.Status = exhausted ? OutboxJobStatus.DeadLettered : OutboxJobStatus.Failed;
            job.LastError = ex.Message;
            job.RunAfter = DateTime.UtcNow.AddSeconds(Backoff(job.Attempts));
            if (exhausted) job.CompletedAt = DateTime.UtcNow;

            _logger.Log(
                exhausted ? LogLevel.Error : LogLevel.Warning,
                ex,
                "Outbox job {JobId} ({Type}) failed on attempt {Attempt}{Fate}",
                jobId,
                type,
                job.Attempts,
                exhausted ? "; dead-lettered" : "");
        }

        await db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Doubles per attempt, capped so a dead-lettering job does not take a day to get there.</summary>
    private int Backoff(int attempt) =>
        (int)Math.Min(_options.BackoffSeconds * Math.Pow(2, Math.Max(0, attempt - 1)), 900);
}
