using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mizan.Application.Ai;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Ai;

/// <summary>
/// Two ceilings, both enforced before any provider call: the caller's daily
/// allowance by tier, and a global daily ceiling that is a circuit breaker on
/// the whole bill (docs/REFOCUS.md §10).
///
/// Reserve-then-settle is one ledger row rather than a Redis counter beside
/// one. §10 sketched Redis counters for the hot path; a single indexed
/// aggregate in front of a call that takes seconds is not a hot path, and one
/// store means no skew and nothing to rebuild. A reservation is written with
/// the estimate and Pending, then updated with the truth. A process that dies
/// mid-call leaves the estimate standing, which over-counts - the safe
/// direction for a spend ceiling.
/// </summary>
public class AiQuotaService : IAiQuotaService
{
    private readonly IMizanDbContext _context;
    private readonly IEntitlementService _entitlements;
    private readonly AiOptions _options;

    public AiQuotaService(
        IMizanDbContext context,
        IEntitlementService entitlements,
        IOptions<AiOptions> options)
    {
        _context = context;
        _entitlements = entitlements;
        _options = options.Value;
    }

    public async Task<AiQuotaLease> ReserveAsync(
        Guid userId,
        Guid? householdId,
        string feature,
        int estimatedTokens,
        Guid? promptVersionId = null,
        CancellationToken cancellationToken = default)
    {
        var (windowStart, windowEnd) = Today();
        var limits = await LimitsForAsync(userId, feature, cancellationToken);

        var mine = await UsageSinceAsync(userId, windowStart, feature == AiFeatures.Eval, cancellationToken);
        if (mine.Requests >= limits.DailyRequests || mine.Tokens + estimatedTokens > limits.DailyTokens)
        {
            throw new AiQuotaExceededException(AiQuotaScope.User, windowEnd);
        }

        var everyone = await GlobalUsageSinceAsync(windowStart, cancellationToken);
        if (everyone.Tokens + estimatedTokens > _options.GlobalDailyTokens
            || everyone.CostMicros >= _options.GlobalDailyCostMicros)
        {
            throw new AiQuotaExceededException(AiQuotaScope.Global, windowEnd);
        }

        var reservation = new AiUsageLog
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            HouseholdId = householdId,
            Feature = feature,
            Model = _options.Model,
            PromptTokens = estimatedTokens,
            CompletionTokens = 0,
            EstimatedCostMicros = CostMicros(estimatedTokens, 0),
            PromptVersionId = promptVersionId,
            Outcome = AiCallOutcome.Pending,
            CreatedAt = DateTime.UtcNow,
        };

        _context.AiUsageLogs.Add(reservation);
        await _context.SaveChangesAsync(cancellationToken);

        return new AiQuotaLease(reservation.Id, userId, householdId, feature, estimatedTokens);
    }

    public async Task SettleAsync(
        AiQuotaLease lease,
        AiTokenUsage usage,
        string model,
        int latencyMs,
        AiCallOutcome outcome,
        CancellationToken cancellationToken = default)
    {
        var cost = CostMicros(usage.PromptTokens, usage.CompletionTokens);

        // Settling a row that is no longer Pending is a no-op, so a retried
        // settle cannot double-count.
        await _context.AiUsageLogs
            .Where(log => log.Id == lease.Id && log.Outcome == AiCallOutcome.Pending)
            .ExecuteUpdateAsync(log => log
                .SetProperty(x => x.PromptTokens, usage.PromptTokens)
                .SetProperty(x => x.CompletionTokens, usage.CompletionTokens)
                .SetProperty(x => x.EstimatedCostMicros, cost)
                .SetProperty(x => x.Model, model)
                .SetProperty(x => x.LatencyMs, latencyMs)
                .SetProperty(x => x.Outcome, outcome),
                cancellationToken);
    }

    public async Task<AiQuotaSnapshot> GetUserSnapshotAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var (windowStart, windowEnd) = Today();
        var entitlement = await _entitlements.GetAsync(userId, cancellationToken);
        var limits = entitlement.IsPro ? _options.Pro : _options.Free;
        var mine = await UsageSinceAsync(userId, windowStart, evalLine: false, cancellationToken);

        return new AiQuotaSnapshot(
            mine.Requests,
            limits.DailyRequests,
            mine.Tokens,
            limits.DailyTokens,
            windowEnd,
            entitlement.Plan);
    }

    /// <summary>UTC days. One boundary everyone shares beats a per-user timezone nobody can reason about.</summary>
    private static (DateTime Start, DateTime End) Today()
    {
        var start = DateTime.UtcNow.Date;
        return (start, start.AddDays(1));
    }

    /// <summary>
    /// Evals get their own line rather than eating the admin's personal
    /// allowance: proving a draft is operational work, and three chat requests
    /// is not a suite. The global ceiling still applies to both.
    /// </summary>
    private async Task<AiTierLimits> LimitsForAsync(Guid userId, string feature, CancellationToken cancellationToken)
    {
        if (feature == AiFeatures.Eval) return _options.Eval;

        var entitlement = await _entitlements.GetAsync(userId, cancellationToken);
        return entitlement.IsPro ? _options.Pro : _options.Free;
    }

    /// <summary>
    /// Usage on the same line the limit came from, so an eval run cannot spend
    /// the admin's chat allowance or the other way round.
    /// </summary>
    private async Task<(int Requests, int Tokens)> UsageSinceAsync(
        Guid userId, DateTime since, bool evalLine, CancellationToken cancellationToken)
    {
        var totals = await _context.AiUsageLogs.AsNoTracking()
            .Where(log => log.UserId == userId
                && log.CreatedAt >= since
                && (log.Feature == AiFeatures.Eval) == evalLine)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Requests = g.Count(),
                Tokens = g.Sum(log => log.PromptTokens + log.CompletionTokens),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return (totals?.Requests ?? 0, totals?.Tokens ?? 0);
    }

    private async Task<(long Tokens, long CostMicros)> GlobalUsageSinceAsync(
        DateTime since, CancellationToken cancellationToken)
    {
        var totals = await _context.AiUsageLogs.AsNoTracking()
            .Where(log => log.CreatedAt >= since)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Tokens = g.Sum(log => (long)log.PromptTokens + log.CompletionTokens),
                Cost = g.Sum(log => log.EstimatedCostMicros),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return (totals?.Tokens ?? 0, totals?.Cost ?? 0);
    }

    private long CostMicros(int promptTokens, int completionTokens) =>
        (promptTokens * _options.PromptCostPerMillionMicros / 1_000_000)
        + (completionTokens * _options.CompletionCostPerMillionMicros / 1_000_000);
}
