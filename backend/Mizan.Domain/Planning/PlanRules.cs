namespace Mizan.Domain.Planning;

/// <summary>
/// The rules a plan is held to. Stored as one JSON document on the plan. Code
/// validates and evaluates these; the model never does. A strict plan is a plan
/// with rules, and <see cref="PlanRuleCheck"/> reports every violation the same
/// way on every surface.
/// </summary>
public class PlanRules
{
    public const decimal KetoMaxCarbsGrams = 30m;

    public MacroMode MacroMode { get; set; } = MacroMode.None;

    /// <summary>Daily targets. Null means no target.</summary>
    public decimal? TargetCalories { get; set; }
    public decimal? TargetProtein { get; set; }
    public decimal? MaxCarbs { get; set; }
    public decimal? TargetFat { get; set; }

    /// <summary>The hours eating is planned for, in the person's time zone. Shown on the plan and used for the evening reminder.</summary>
    public TimeOnly? EatingWindowStart { get; set; }
    public TimeOnly? EatingWindowEnd { get; set; }

    /// <summary>When not empty, every food in an entry or a recipe must be on this list.</summary>
    public List<Guid> AllowedFoodIds { get; set; } = [];

    /// <summary>A food on this list in an entry or a recipe is a violation.</summary>
    public List<Guid> ForbiddenFoodIds { get; set; } = [];

    /// <summary>The weekly ceiling for costing, in <see cref="Currency"/>.</summary>
    public decimal? WeeklyBudget { get; set; }

    /// <summary>ISO 4217 code, for example ETB.</summary>
    public string? Currency { get; set; }

    public PrepStyle PrepStyle { get; set; } = PrepStyle.Daily;

    /// <summary>Day indexes on which batch cooking happens, when <see cref="PrepStyle"/> is Batch.</summary>
    public List<int> BatchDays { get; set; } = [];

    /// <summary>The carb ceiling in force: the explicit one, else the mode's default.</summary>
    public decimal? EffectiveMaxCarbs => MaxCarbs ?? (MacroMode == MacroMode.Keto ? KetoMaxCarbsGrams : null);

    public bool HasAnyRule =>
        MacroMode != MacroMode.None
        || TargetCalories.HasValue || TargetProtein.HasValue || MaxCarbs.HasValue || TargetFat.HasValue
        || EatingWindowStart.HasValue || EatingWindowEnd.HasValue
        || AllowedFoodIds.Count > 0 || ForbiddenFoodIds.Count > 0
        || WeeklyBudget.HasValue
        || PrepStyle != PrepStyle.Daily;

    /// <summary>
    /// Problems with the rules themselves, as messages a person can act on.
    /// Empty when the rules are usable.
    /// </summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        if (TargetCalories is <= 0) problems.Add("Target calories must be above zero.");
        if (TargetProtein is < 0) problems.Add("Target protein cannot be negative.");
        if (MaxCarbs is < 0) problems.Add("Maximum carbs cannot be negative.");
        if (TargetFat is < 0) problems.Add("Target fat cannot be negative.");
        if (WeeklyBudget is < 0) problems.Add("Weekly budget cannot be negative.");
        if (WeeklyBudget.HasValue && string.IsNullOrWhiteSpace(Currency)) problems.Add("A weekly budget needs a currency.");
        if (Currency is { Length: not 3 }) problems.Add("Currency must be a three-letter code.");
        if (EatingWindowStart.HasValue != EatingWindowEnd.HasValue) problems.Add("An eating window needs both a start and an end.");
        if (EatingWindowStart.HasValue && EatingWindowEnd.HasValue && EatingWindowStart >= EatingWindowEnd) problems.Add("The eating window must end after it starts.");
        if (PrepStyle == PrepStyle.Batch && BatchDays.Count == 0) problems.Add("Batch cooking needs at least one cooking day.");
        if (BatchDays.Any(d => d < 0)) problems.Add("Cooking days are counted from the first day of the plan.");
        if (AllowedFoodIds.Intersect(ForbiddenFoodIds).Any()) problems.Add("A food cannot be both allowed and forbidden.");
        return problems;
    }
}
