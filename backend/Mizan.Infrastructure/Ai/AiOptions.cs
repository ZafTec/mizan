using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Ai;

public class AiOptions : IAiCeilings
{
    public const string SectionName = "Ai";

    /// <summary>OpenAI-compatible endpoint, e.g. https://api.example/v1. Empty disables the assistant.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-5.6-luna";
    public int MaxOutputTokens { get; set; } = 1024;
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Per-day allowances by tier. Free gets enough to see the value.</summary>
    public AiTierLimits Free { get; set; } = new() { DailyRequests = 5, DailyTokens = 20_000 };

    public AiTierLimits Pro { get; set; } = new() { DailyRequests = 200, DailyTokens = 500_000 };

    /// <summary>
    /// The eval budget line. An admin proving a draft is not doing it on their
    /// personal allowance, but it is still a line inside the global ceiling -
    /// a runaway suite stops in the same place a runaway user does.
    /// </summary>
    public AiTierLimits Eval { get; set; } = new() { DailyRequests = 400, DailyTokens = 400_000 };

    /// <summary>
    /// Setting a new user up. One turn can be several provider calls, so this
    /// is generous by design: onboarding is the surface the assistant justifies
    /// itself on, and it must not be the surface that exhausts a free
    /// allowance before the user has seen anything.
    /// </summary>
    public AiTierLimits Onboarding { get; set; } = new() { DailyRequests = 60, DailyTokens = 60_000 };

    /// <summary>
    /// The circuit breaker on the whole provider bill. Not optional: a loop or
    /// an abusive account stops here rather than at the invoice.
    /// </summary>
    public long GlobalDailyTokens { get; set; } = 5_000_000;

    public long GlobalDailyCostMicros { get; set; } = 20_000_000;

    /// <summary>Provider pricing, in millionths of a currency unit per million tokens.</summary>
    public long PromptCostPerMillionMicros { get; set; } = 150_000;

    public long CompletionCostPerMillionMicros { get; set; } = 600_000;
}

public class AiTierLimits
{
    public int DailyRequests { get; set; }
    public int DailyTokens { get; set; }
}
