namespace Mizan.Contracts.Admin;

/// <summary>One group in the platform usage view. <c>Key</c> is the id (or feature name) the group stands for; null for "no household".</summary>
public record PlatformUsageRowDto
{
    public string? Key { get; init; }
    public string Label { get; init; } = string.Empty;
    public int Requests { get; init; }
    public long Tokens { get; init; }
    public long CostMicros { get; init; }

    /// <summary>The same cost in whole currency units, for display.</summary>
    public decimal Cost { get; init; }

    /// <summary>Calls the allowances refused. They cost nothing and are not counted as requests.</summary>
    public int Refusals { get; init; }
}

/// <summary>GET /api/admin/usage: the AI usage ledger between two UTC dates, inclusive, grouped by household, user, plan or feature.</summary>
public record PlatformUsageDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public string GroupBy { get; init; } = "household";
    public IReadOnlyList<PlatformUsageRowDto> Rows { get; init; } = [];
    public PlatformUsageRowDto Total { get; init; } = new();
}
