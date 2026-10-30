using Mizan.Application.Ai;
using Mizan.Domain.Usage;

namespace Mizan.Application.Interfaces;

/// <summary>
/// The configured tier allowance for a line, before any override. Read per call, because an
/// administrator can change it at run time (docs/ADMIN.md#runtime-settings).
/// </summary>
public interface IAiTierDefaults
{
    Task<UsageLimitValues> ForAsync(AiQuotaLine line, bool isPro, CancellationToken cancellationToken = default);
}

/// <summary>
/// The caps in force for one user on one line, with the household and plan they
/// were decided for. <see cref="IAiQuotaService"/> enforces exactly these.
/// </summary>
public sealed record EffectiveUsageLimits(ComposedUsageLimits Limits, Guid? HouseholdId, Guid? PlanId, bool IsPro)
{
    public int DailyRequests => Limits.DailyRequests.Value ?? int.MaxValue;
    public long DailyTokens => Limits.DailyTokens.Value ?? long.MaxValue;
    public long? DailyCostMicros => Limits.DailyCostMicros.Value;
    public long? MonthlyCostMicros => Limits.MonthlyCostMicros.Value;
}

/// <summary>
/// Composes an allowance field by field: the user's override, then their
/// household's, then their billing plan's, then the tier default
/// (<see cref="UsageLimitComposition"/>, docs/AI.md#metering). Within one scope
/// an override for the line wins over one for every line.
/// </summary>
public interface IUsageLimits
{
    /// <summary>A null <paramref name="householdId"/> means the user's active household.</summary>
    Task<EffectiveUsageLimits> ResolveAsync(
        Guid userId, Guid? householdId, AiQuotaLine line, CancellationToken cancellationToken = default);
}
