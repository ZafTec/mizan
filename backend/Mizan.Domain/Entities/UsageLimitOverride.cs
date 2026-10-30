using Mizan.Domain.Usage;

namespace Mizan.Domain.Entities;

/// <summary>
/// An administrator's change to the AI allowance of one user, one household, or
/// every subscriber of one billing plan. A null field inherits from the next
/// scope out, and a null <see cref="Line"/> covers every allowance line
/// (docs/AI.md#metering).
/// </summary>
public class UsageLimitOverride
{
    public Guid Id { get; set; }
    public UsageLimitScope Scope { get; set; }

    /// <summary>A user id, a household id or a billing plan id, by <see cref="Scope"/>.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>The allowance line's name (Personal, Planning, ...), or null for every line.</summary>
    public string? Line { get; set; }

    public int? DailyRequests { get; set; }
    public long? DailyTokens { get; set; }
    public long? DailyCostMicros { get; set; }
    public long? MonthlyCostMicros { get; set; }
    public string? Note { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public UsageLimitValues Values => new(DailyRequests, DailyTokens, DailyCostMicros, MonthlyCostMicros);
}
