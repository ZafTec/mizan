using Microsoft.EntityFrameworkCore;
using Mizan.Application.Ai;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Ai;

/// <summary>
/// Two ceilings, both enforced before any provider call: the caller's daily
/// allowance by tier, and a global daily ceiling that is a circuit breaker on
/// the whole bill (docs/AI.md).
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
    private readonly IRuntimeOptions<AiOptions> _settings;

    public AiQuotaService(
        IMizanDbContext context,
        IEntitlementService entitlements,
        IRuntimeOptions<AiOptions> settings)
    {
        _context = context;
        _entitlements = entitlements;
        _settings = settings;
    }

    public async Task<AiQuotaLease> ReserveAsync(
        Guid userId,
        Guid? householdId,
        string feature,
        int estimatedTokens,
        Guid? promptVersionId = null,
        CancellationToken cancellationToken = default)
    {
        // Ceilings and tier limits are read per call, so a limit changed in the admin area holds
        // from the next reservation.
        var options = await _settings.GetAsync(cancellationToken);
        var (windowStart, windowEnd) = Today();
        var line = AiQuotaLines.For(feature);
        var limits = await LimitsForAsync(options, userId, line, cancellationToken);

        var mine = await UsageSinceAsync(userId, windowStart, line, cancellationToken);
        if (mine.Requests >= limits.DailyRequests || mine.Tokens + estimatedTokens > limits.DailyTokens)
        {
            throw new AiQuotaExceededException(AiQuotaScope.User, windowEnd);
        }

        var everyone = await GlobalUsageSinceAsync(windowStart, cancellationToken);
        if (everyone.Tokens + estimatedTokens > options.GlobalDailyTokens
            || everyone.CostMicros >= options.GlobalDailyCostMicros)
        {
            throw new AiQuotaExceededException(AiQuotaScope.Global, windowEnd);
        }

        var reservation = new AiUsageLog
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            HouseholdId = householdId,
            Feature = feature,
            Model = options.Model,
            PromptTokens = estimatedTokens,
            CompletionTokens = 0,
            EstimatedCostMicros = CostMicros(options, estimatedTokens, 0),
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
        var options = await _settings.GetAsync(cancellationToken);
        var cost = CostMicros(options, usage.PromptTokens, usage.CompletionTokens);

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

    public Task<AiQuotaSnapshot> GetUserSnapshotAsync(Guid userId, CancellationToken cancellationToken = default) =>
        GetUserSnapshotAsync(userId, AiQuotaLine.Personal, cancellationToken);

    public async Task<AiQuotaSnapshot> GetUserSnapshotAsync(
        Guid userId, AiQuotaLine line, CancellationToken cancellationToken = default)
    {
        var (windowStart, windowEnd) = Today();
        var options = await _settings.GetAsync(cancellationToken);
        var entitlement = await _entitlements.GetAsync(userId, cancellationToken);
        var limits = await LimitsForAsync(options, userId, line, cancellationToken);
        var mine = await UsageSinceAsync(userId, windowStart, line, cancellationToken);

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

    private async Task<AiTierLimits> LimitsForAsync(
        AiOptions options, Guid userId, AiQuotaLine line, CancellationToken cancellationToken)
    {
        if (line == AiQuotaLine.Eval) return options.Eval;
        if (line == AiQuotaLine.Onboarding) return options.Onboarding;
        if (line == AiQuotaLine.TrainerClient) return options.Trainer;

        var entitlement = await _entitlements.GetAsync(userId, cancellationToken);
        if (line == AiQuotaLine.Planning) return entitlement.IsPro ? options.PlanningPro : options.PlanningFree;
        return entitlement.IsPro ? options.Pro : options.Free;
    }

    /// <summary>
    /// Usage on the same line the limit came from, so an eval run or a setup
    /// conversation cannot spend the caller's chat allowance, or the reverse.
    /// </summary>
    private async Task<(int Requests, int Tokens)> UsageSinceAsync(
        Guid userId, DateTime since, AiQuotaLine line, CancellationToken cancellationToken)
    {
        var query = _context.AiUsageLogs.AsNoTracking()
            .Where(log => log.UserId == userId && log.CreatedAt >= since);

        query = line switch
        {
            AiQuotaLine.Eval => query.Where(log => log.Feature == AiFeatures.Eval),
            AiQuotaLine.Onboarding => query.Where(log => log.Feature == AiFeatures.Onboarding),
            AiQuotaLine.TrainerClient => query.Where(log => log.Feature == AiFeatures.TrainerClient),
            AiQuotaLine.Planning => query.Where(log => log.Feature == AiFeatures.Planning),
            _ => query.Where(log =>
                log.Feature != AiFeatures.Eval
                && log.Feature != AiFeatures.Onboarding
                && log.Feature != AiFeatures.TrainerClient
                && log.Feature != AiFeatures.Planning),
        };

        var totals = await query
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

    private static long CostMicros(AiOptions options, int promptTokens, int completionTokens) =>
        (promptTokens * options.PromptCostPerMillionMicros / 1_000_000)
        + (completionTokens * options.CompletionCostPerMillionMicros / 1_000_000);
}
