namespace Mizan.Contracts.Planning;

/// <summary>
/// Body of POST /api/MealPlans/generate. Each budget ceiling is one metered call and one draft;
/// with none, one draft is made against <c>Rules.WeeklyBudget</c> if it is set. The preference is
/// the person's own words and reaches the model as data.
/// </summary>
public record GenerateMealPlanRequest
{
    public string? Name { get; init; }
    public DateOnly? StartDate { get; init; }
    public int DurationDays { get; init; } = 7;
    public PlanRulesDto? Rules { get; init; }
    public string? Preference { get; init; }

    /// <summary>Weekly budgets in the rules' currency, or the price region's when the rules name none. At most three.</summary>
    public List<decimal> BudgetCeilings { get; init; } = [];

    /// <summary>
    /// A ceiling for each day's cost, in the same currency as the budget ceilings. It overrides
    /// <c>Rules.DailyBudget</c>, applies to every draft, and is told to the model and checked on
    /// each day of the draft.
    /// </summary>
    public decimal? DailyBudget { get; init; }
}

/// <summary>Where a generated plan came from. Null on a plan a person or a coach wrote.</summary>
public record PlanGenerationDto(
    string PromptKey,
    Guid? PromptVersionId,
    int? PromptVersion,
    string Model,
    decimal? BudgetCeiling,
    string? Currency,
    DateTime GeneratedAt);

/// <summary>One shopping line of a plan's cost. <c>Cost</c> is null when the line has no price, and <c>Reason</c> says why.</summary>
public record PlanCostItemDto(
    string Name,
    Guid? FoodId,
    decimal? Grams,
    decimal? Cost,
    string? Confidence,
    string? Reason);

/// <summary>
/// What a plan costs in the person's price region. <c>WeeklyTotal</c> scales the total to seven
/// days so plans of any length compare with a weekly budget. <c>Coverage</c> is the share of lines
/// that carry a price, 0 to 1, and <c>LowestConfidence</c> the weakest price used.
/// </summary>
public record PlanCostDto(
    string RegionCode,
    string Currency,
    decimal Total,
    decimal WeeklyTotal,
    decimal? WeeklyBudget,
    int PricedItems,
    int TotalItems,
    decimal Coverage,
    string? LowestConfidence,
    IReadOnlyList<PlanCostItemDto> Items);

/// <summary>A line the model wrote that did not become a food or recipe entry. It is saved as a text entry for the person to review.</summary>
public record UnresolvedPlanLineDto(int DayIndex, string Slot, string Text, string Reason);

/// <summary>One draft: the plan as the plan page shows it, its violations, its cost table, and the lines left for review.</summary>
public record GeneratedPlanDto
{
    public MealPlanDetailDto Plan { get; init; } = new();
    public decimal? BudgetCeiling { get; init; }
    public List<PlanViolationDto> Violations { get; init; } = [];
    public PlanCostDto Cost { get; init; } = new("", "", 0, 0, null, 0, 0, 0, null, []);
    public List<UnresolvedPlanLineDto> Unresolved { get; init; } = [];

    /// <summary>The model's one-line note on the draft, shown as plain text.</summary>
    public string? Note { get; init; }
}

/// <summary>A budget ceiling that got no draft, and why: usually the day's planning allowance ran out after the first.</summary>
public record SkippedBudgetDto(decimal BudgetCeiling, string Reason);

/// <summary>Response of POST /api/MealPlans/generate: one draft per budget ceiling, each saved with status Draft.</summary>
public record GenerateMealPlanResult
{
    public List<GeneratedPlanDto> Drafts { get; init; } = [];
    public List<SkippedBudgetDto> Skipped { get; init; } = [];
}

/// <summary>
/// The 422 body of POST /api/MealPlans/generate when the draft would have days with no food or
/// recipe: <c>errorCode</c> is <c>plan_needs_foods</c>, <c>emptyDays</c> are day indexes from 0,
/// and <c>missingFoods</c> the names the assistant used that the catalogue does not have.
/// Nothing is saved.
/// </summary>
public record PlanNeedsFoodsError(string ErrorCode, string Error, List<string> MissingFoods, List<int> EmptyDays);
