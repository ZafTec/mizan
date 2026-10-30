using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Admin;
using Mizan.Contracts.Prices;
using Mizan.Domain.Admin;
using Mizan.Domain.Entities;

namespace Mizan.Application.Admin;

/// <summary>
/// GET /api/admin/overview/health: one line per service the app depends on.
/// The numbers come from the database and the probes; what they mean comes
/// from <see cref="SystemHealthRules"/>. A service with nothing configured is
/// reported as notConfigured, never as a green tile. Cached for 30 seconds,
/// so a page left open does not ping every service on every refresh.
/// </summary>
public record GetSystemHealthQuery : IRequest<SystemHealthDto>;

public class GetSystemHealthQueryHandler : IRequestHandler<GetSystemHealthQuery, SystemHealthDto>
{
    public const string CacheKey = "admin:system-health";

    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(30),
        LocalCacheExpiration = TimeSpan.FromSeconds(30),
    };

    private readonly IMizanDbContext _context;
    private readonly ISystemProbes _probes;
    private readonly IAiCeilings _ceilings;
    private readonly HybridCache _cache;

    public GetSystemHealthQueryHandler(IMizanDbContext context, ISystemProbes probes, IAiCeilings ceilings, HybridCache cache)
    {
        _context = context;
        _probes = probes;
        _ceilings = ceilings;
        _cache = cache;
    }

    public async Task<SystemHealthDto> Handle(GetSystemHealthQuery request, CancellationToken cancellationToken) =>
        await _cache.GetOrCreateAsync(
            CacheKey,
            this,
            static (self, ct) => self.MeasureAsync(ct),
            CacheOptions,
            cancellationToken: cancellationToken);

    private async ValueTask<SystemHealthDto> MeasureAsync(CancellationToken cancellationToken)
    {
        var checks = new List<SystemHealthCheckDto>();
        void Add(string name, HealthVerdict verdict) =>
            checks.Add(new SystemHealthCheckDto(name, Wire(verdict.Status), verdict.Detail, DateTime.UtcNow));

        var database = await _probes.DatabaseAsync(cancellationToken);
        Add("database", SystemHealthRules.RoundTrip(database.Configured, database.Elapsed, database.Error));

        var redis = await _probes.RedisAsync(cancellationToken);
        Add("redis", SystemHealthRules.RoundTrip(redis.Configured, redis.Elapsed, redis.Error));

        // Everything below reads the database; without it there is nothing to say.
        if (database.Elapsed is null)
        {
            foreach (var name in new[] { "outbox", "email", "ai" })
                checks.Add(new SystemHealthCheckDto(name, Wire(SystemHealthStatus.Down), "The database did not answer.", DateTime.UtcNow));
        }
        else
        {
            var now = DateTime.UtcNow;
            var pending = await _context.OutboxJobs.AsNoTracking()
                .CountAsync(j => j.Status == OutboxJobStatus.Pending || j.Status == OutboxJobStatus.Failed, cancellationToken);
            var deadLettered = await _context.OutboxJobs.AsNoTracking()
                .CountAsync(j => j.Status == OutboxJobStatus.DeadLettered, cancellationToken);
            var oldest = await _context.OutboxJobs.AsNoTracking()
                .Where(j => (j.Status == OutboxJobStatus.Pending || j.Status == OutboxJobStatus.Failed) && j.RunAfter <= now)
                .OrderBy(j => j.RunAfter)
                .Select(j => (DateTime?)j.RunAfter)
                .FirstOrDefaultAsync(cancellationToken);
            Add("outbox", SystemHealthRules.Outbox(pending, deadLettered, oldest is { } at ? now - at : null));

            var dayAgo = now.AddDays(-1);
            var failedMail = await _context.EmailMessages.AsNoTracking()
                .CountAsync(m => m.Status == EmailMessageStatus.Failed && m.CreatedAt >= dayAgo, cancellationToken);
            Add("email", SystemHealthRules.Email(await _probes.SmtpConfiguredAsync(cancellationToken), failedMail));

            var today = now.Date;
            var usage = await _context.AiUsageLogs.AsNoTracking()
                .Where(l => l.CreatedAt >= today)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Tokens = g.Sum(l => (long)l.PromptTokens + l.CompletionTokens),
                    Cost = g.Sum(l => l.EstimatedCostMicros),
                })
                .FirstOrDefaultAsync(cancellationToken);
            var ceilings = await _ceilings.GetAsync(cancellationToken);
            Add("ai", SystemHealthRules.Ai(
                await _probes.AiConfiguredAsync(cancellationToken),
                usage?.Tokens ?? 0,
                ceilings.GlobalDailyTokens,
                usage?.Cost ?? 0,
                ceilings.GlobalDailyCostMicros));
        }

        var telegram = await _probes.TelegramBotAsync(cancellationToken);
        Add("telegram", SystemHealthRules.Reachable(telegram.Configured, telegram.Configured ? telegram.Elapsed is not null : null, "bot service"));

        Add("push", SystemHealthRules.Configured(await _probes.PushConfiguredAsync(cancellationToken), "Push"));

        DateTime? lastPriceRun = null;
        if (_probes.PricesWorkerConfigured && database.Elapsed is not null)
        {
            lastPriceRun = await _context.PriceSources.AsNoTracking()
                .Where(s => s.LastStatus == PriceRunStatus.Succeeded && s.LastRunAt != null)
                .MaxAsync(s => s.LastRunAt, cancellationToken);
        }
        Add("prices", SystemHealthRules.Prices(_probes.PricesWorkerConfigured, lastPriceRun, DateTime.UtcNow));

        Add("storage", SystemHealthRules.Configured(await _probes.StorageConfiguredAsync(cancellationToken), "Object storage"));

        return new SystemHealthDto(checks, DateTime.UtcNow);
    }

    /// <summary>The spelling on the wire: ok, degraded, down, notConfigured.</summary>
    public static string Wire(SystemHealthStatus status) => status switch
    {
        SystemHealthStatus.Ok => "ok",
        SystemHealthStatus.Degraded => "degraded",
        SystemHealthStatus.Down => "down",
        _ => "notConfigured",
    };
}
