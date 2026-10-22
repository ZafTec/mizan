namespace Mizan.Domain.Planning;

/// <summary>
/// Where a generated plan came from: which prompt and model wrote its entries and the budget it
/// was asked to stay under. Stored as one JSON document on the plan, so a bad draft can be traced
/// to the exact prompt version (docs/AI.md#metering). Null on a plan a person or a coach wrote.
/// </summary>
public class PlanGeneration
{
    /// <summary>The prompt key, <c>meal.plan</c>.</summary>
    public string PromptKey { get; set; } = string.Empty;

    /// <summary>The published version, or null when the built-in default answered.</summary>
    public Guid? PromptVersionId { get; set; }

    public int? PromptVersion { get; set; }

    /// <summary>The model the provider reported.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>The weekly budget this draft was generated for, when the request named one.</summary>
    public decimal? BudgetCeiling { get; set; }

    public string? Currency { get; set; }

    public DateTime GeneratedAt { get; set; }
}
