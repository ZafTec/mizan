using FluentAssertions;
using Mizan.Domain.Planning;
using Xunit;

namespace Mizan.Tests.PlanningRules;

public class MacroTargetsTests
{
    private static readonly GoalTargets WeightLoss = new(Calories: 1800, Protein: 120, Carbs: 150, Fat: 60);
    private static readonly GoalTargets LowProteinGain = new(Calories: 3000, Protein: 150, Carbs: 380, Fat: 90);
    private static readonly GoalTargets CaloriesOnly = new(Calories: 2000, Protein: null, Carbs: null, Fat: null);

    private static PlanRules Rules(MacroMode mode, decimal? kcal = null, decimal? protein = null, decimal? carbs = null, decimal? fat = null) =>
        new() { MacroMode = mode, TargetCalories = kcal, TargetProtein = protein, MaxCarbs = carbs, TargetFat = fat };

    // Expected: (value, source) for calories, protein, carbs, fat; then goalMissing.
    public static TheoryData<string, PlanRules, GoalTargets?, (decimal?, string?)[], bool> Cases => new()
    {
        { "None without targets has none", Rules(MacroMode.None), WeightLoss,
            [(null, null), (null, null), (null, null), (null, null)], false },
        { "None keeps what was typed", Rules(MacroMode.None, kcal: 2200), WeightLoss,
            [(2200, "typed"), (null, null), (null, null), (null, null)], false },
        { "Custom ignores the goal", Rules(MacroMode.Custom, protein: 90), WeightLoss,
            [(null, null), (90, "typed"), (null, null), (null, null)], false },
        { "Keto without a goal caps carbs and says the goal is missing", Rules(MacroMode.Keto), null,
            [(null, null), (null, null), (30, "mode"), (null, null)], true },
        { "Keto with a goal takes calories, protein and fat from it", Rules(MacroMode.Keto), WeightLoss,
            [(1800, "goal"), (120, "goal"), (30, "mode"), (60, "goal")], false },
        { "Keto with a typed carb cap keeps it", Rules(MacroMode.Keto, carbs: 50), WeightLoss,
            [(1800, "goal"), (120, "goal"), (50, "typed"), (60, "goal")], false },
        { "HighProtein without a goal has no targets and says so", Rules(MacroMode.HighProtein), null,
            [(null, null), (null, null), (null, null), (null, null)], true },
        { "HighProtein lifts the goal protein to 30% of goal calories", Rules(MacroMode.HighProtein), WeightLoss,
            [(1800, "goal"), (135, "derived"), (null, null), (null, null)], false },
        { "HighProtein lifts a gain goal protein to 30% of calories", Rules(MacroMode.HighProtein), LowProteinGain,
            [(3000, "goal"), (225, "derived"), (null, null), (null, null)], false },
        { "HighProtein derives protein from goal calories alone", Rules(MacroMode.HighProtein), CaloriesOnly,
            [(2000, "goal"), (150, "derived"), (null, null), (null, null)], false },
        { "HighProtein derives protein from typed calories", Rules(MacroMode.HighProtein, kcal: 2500), null,
            [(2500, "typed"), (188, "derived"), (null, null), (null, null)], true },
        { "HighProtein keeps typed protein", Rules(MacroMode.HighProtein, protein: 100), WeightLoss,
            [(1800, "goal"), (100, "typed"), (null, null), (null, null)], false },
        { "HighProtein uses goal protein when it beats the share", Rules(MacroMode.HighProtein, kcal: 1200), WeightLoss,
            [(1200, "typed"), (120, "goal"), (null, null), (null, null)], false },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Resolves_the_effective_targets(string because, PlanRules rules, GoalTargets? goal, (decimal?, string?)[] expected, bool goalMissing)
    {
        var targets = MacroTargets.Resolve(rules, goal);

        new[] { targets.Calories, targets.Protein, targets.MaxCarbs, targets.Fat }
            .Select(t => (t.Value, t.Source))
            .Should().Equal(expected, because);
        targets.GoalMissing.Should().Be(goalMissing, because);
    }
}
