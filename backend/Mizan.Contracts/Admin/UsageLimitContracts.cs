namespace Mizan.Contracts.Admin;

/// <summary>
/// One stored override. <c>Line</c> is the allowance line (Personal, Planning,
/// Onboarding, TrainerClient, Eval) or null for every line. A null cap inherits.
/// </summary>
public record UsageLimitOverrideDto
{
    public Guid Id { get; init; }
    public string Scope { get; init; } = string.Empty;
    public Guid SubjectId { get; init; }
    public string? Line { get; init; }
    public int? DailyRequests { get; init; }
    public long? DailyTokens { get; init; }
    public long? DailyCostMicros { get; init; }
    public long? MonthlyCostMicros { get; init; }
    public string? Note { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>A cap in force and where it came from: Default, Plan, Household or User. A null value is no cap.</summary>
public record SourcedLimitDto(long? Value, string Source);

/// <summary>What one allowance line allows the subject, field by field.</summary>
public record EffectiveUsageLimitDto
{
    public string Line { get; init; } = string.Empty;
    public SourcedLimitDto DailyRequests { get; init; } = new(null, "Default");
    public SourcedLimitDto DailyTokens { get; init; } = new(null, "Default");
    public SourcedLimitDto DailyCostMicros { get; init; } = new(null, "Default");
    public SourcedLimitDto MonthlyCostMicros { get; init; } = new(null, "Default");
}

/// <summary>
/// A plan, household or user with its overrides and the limits they produce.
/// A plan or household is shown against the tier its members are on by default
/// (Pro for a plan, Free for a household); a user against their own tier,
/// household and plan.
/// </summary>
public record UsageLimitSubjectDto
{
    public string Scope { get; init; } = string.Empty;
    public Guid SubjectId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Detail { get; init; }

    /// <summary>free or pro: the tier whose defaults the effective limits fall back to.</summary>
    public string Tier { get; init; } = "free";
    public IReadOnlyList<UsageLimitOverrideDto> Overrides { get; init; } = [];
    public IReadOnlyList<EffectiveUsageLimitDto> Effective { get; init; } = [];
}

/// <summary>Body of PUT /api/admin/usage-limits/{scope}/{subjectId}. The pair of subject and line is unique; a second PUT replaces it.</summary>
public record SetUsageLimitRequest(
    string? Line,
    int? DailyRequests,
    long? DailyTokens,
    long? DailyCostMicros,
    long? MonthlyCostMicros,
    string? Note);
