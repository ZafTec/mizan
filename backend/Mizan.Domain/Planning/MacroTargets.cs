namespace Mizan.Domain.Planning;

/// <summary>The daily targets of the person's current goal, as far as they are set.</summary>
public sealed record GoalTargets(decimal? Calories, decimal? Protein, decimal? Carbs, decimal? Fat);

/// <summary>Where an effective target came from. Stable names for clients.</summary>
public static class TargetSources
{
    /// <summary>The person typed it into the plan's rules. A typed value always wins.</summary>
    public const string Typed = "typed";

    /// <summary>The macro mode's own default, such as Keto's carb ceiling.</summary>
    public const string Mode = "mode";

    /// <summary>The person's current goal.</summary>
    public const string Goal = "goal";

    /// <summary>Computed from the other targets by the mode's rule, such as HighProtein's protein share of calories.</summary>
    public const string Derived = "derived";
}

/// <summary>One effective daily target and its source. Both are null when the plan has no such target.</summary>
public sealed record EffectiveTarget(decimal? Value, string? Source)
{
    public static readonly EffectiveTarget None = new(null, null);
}

/// <summary>
/// The targets a plan is held to once its macro mode and the person's goal are applied.
/// <see cref="GoalMissing"/> is true when the mode takes targets from a goal and there is none
/// the viewer may read, so the surface can say so and link to setting one.
/// </summary>
public sealed record PlanTargets(
    EffectiveTarget Calories,
    EffectiveTarget Protein,
    EffectiveTarget MaxCarbs,
    EffectiveTarget Fat,
    bool GoalMissing);

/// <summary>
/// Resolves the effective daily targets of a plan (docs/PLANNING.md#rules). A value typed into
/// the rules always wins. Then, by mode:
/// <list type="bullet">
/// <item>None and Custom: only the typed values.</item>
/// <item>Keto: carbs at most <see cref="PlanRules.KetoMaxCarbsGrams"/> g; calories, protein and fat from the goal.</item>
/// <item>HighProtein: calories from the goal; protein the larger of the goal's protein and
/// <see cref="HighProteinShare"/> of the calories at 4 kcal a gram, rounded to whole grams.</item>
/// </list>
/// Validation, violations, the generator and every surface use this one answer.
/// </summary>
public static class MacroTargets
{
    /// <summary>The share of calories HighProtein asks to come from protein, at least.</summary>
    public const decimal HighProteinShare = 0.30m;

    public const decimal KcalPerGramProtein = 4m;

    public static PlanTargets Resolve(PlanRules rules, GoalTargets? goal)
    {
        var mode = rules.MacroMode;
        var usesGoal = mode is MacroMode.Keto or MacroMode.HighProtein;

        var calories = Typed(rules.TargetCalories) ?? (usesGoal ? FromGoal(goal?.Calories) : null) ?? EffectiveTarget.None;
        var carbs = Typed(rules.MaxCarbs)
            ?? (mode == MacroMode.Keto ? new EffectiveTarget(PlanRules.KetoMaxCarbsGrams, TargetSources.Mode) : null)
            ?? EffectiveTarget.None;
        var fat = Typed(rules.TargetFat) ?? (mode == MacroMode.Keto ? FromGoal(goal?.Fat) : null) ?? EffectiveTarget.None;

        var protein = Typed(rules.TargetProtein) ?? mode switch
        {
            MacroMode.Keto => FromGoal(goal?.Protein),
            MacroMode.HighProtein => HighProtein(goal?.Protein, calories.Value),
            _ => null,
        } ?? EffectiveTarget.None;

        return new PlanTargets(calories, protein, carbs, fat, GoalMissing: usesGoal && goal is null);
    }

    /// <summary>The goal's protein or the protein share of the calories, whichever is larger.</summary>
    private static EffectiveTarget? HighProtein(decimal? goalProtein, decimal? calories)
    {
        decimal? share = calories is > 0 ? Math.Round(calories.Value * HighProteinShare / KcalPerGramProtein, 0, MidpointRounding.AwayFromZero) : null;
        if (goalProtein is > 0 && (share is null || goalProtein >= share)) return new EffectiveTarget(goalProtein, TargetSources.Goal);
        return share is { } derived ? new EffectiveTarget(derived, TargetSources.Derived) : null;
    }

    private static EffectiveTarget? Typed(decimal? value) => value is { } v ? new EffectiveTarget(v, TargetSources.Typed) : null;

    private static EffectiveTarget? FromGoal(decimal? value) => value is > 0 ? new EffectiveTarget(value, TargetSources.Goal) : null;
}
