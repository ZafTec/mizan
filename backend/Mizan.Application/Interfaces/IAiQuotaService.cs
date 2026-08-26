namespace Mizan.Application.Interfaces;

public enum AiQuotaScope
{
    /// <summary>This user has spent their allowance.</summary>
    User = 0,

    /// <summary>Everyone together has hit the daily ceiling. Not the user's fault, and the message says so.</summary>
    Global = 1,
}

/// <summary>
/// A reservation taken before a call and settled after it. Holding one is what
/// makes a crashed call still count: <see cref="IAiQuotaService.SettleAsync"/>
/// runs in a finally, so tokens cannot leak (docs/REFOCUS.md §10).
/// </summary>
public record AiQuotaLease(Guid Id, Guid UserId, Guid? HouseholdId, string Feature, int EstimatedTokens);

public record AiQuotaSnapshot(
    int RequestsUsed,
    int RequestLimit,
    int TokensUsed,
    int TokenLimit,
    DateTime ResetsAt,
    string Plan);

/// <summary>
/// Two independent ceilings, both of which must pass: the caller's own
/// allowance by tier, and a global daily ceiling that is a circuit breaker on
/// the provider bill.
/// </summary>
public interface IAiQuotaService
{
    /// <summary>
    /// Throws <see cref="Exceptions.AiQuotaExceededException"/> when either
    /// ceiling is spent. Never returns a "denied" result the caller might
    /// forget to check.
    /// </summary>
    Task<AiQuotaLease> ReserveAsync(
        Guid userId,
        Guid? householdId,
        string feature,
        int estimatedTokens,
        CancellationToken cancellationToken = default);

    /// <summary>Records what the call actually cost. Safe to call more than once.</summary>
    Task SettleAsync(
        AiQuotaLease lease,
        AiTokenUsage usage,
        string model,
        int latencyMs,
        Domain.Entities.AiCallOutcome outcome,
        CancellationToken cancellationToken = default);

    Task<AiQuotaSnapshot> GetUserSnapshotAsync(Guid userId, CancellationToken cancellationToken = default);
}
