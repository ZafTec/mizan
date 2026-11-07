using FluentAssertions;
using Mizan.Domain.Planning;
using Xunit;

namespace Mizan.Tests.PlanningRules;

public class DraftFoodCoverageTests
{
    public static TheoryData<string, (int, PlanEntryKind)[], int, int[]> Cases => new()
    {
        { "every day has a food", [(0, PlanEntryKind.Food), (1, PlanEntryKind.Food)], 2, [] },
        { "a recipe covers its day", [(0, PlanEntryKind.Recipe), (1, PlanEntryKind.Food)], 2, [] },
        { "a day of notes is empty", [(0, PlanEntryKind.Food), (1, PlanEntryKind.Text)], 2, [1] },
        { "a day with nothing is empty", [(1, PlanEntryKind.Food)], 3, [0, 2] },
        { "no entries at all", [], 2, [0, 1] },
        { "entries past the end do not cover", [(0, PlanEntryKind.Food), (5, PlanEntryKind.Food)], 2, [1] },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Finds_the_days_without_a_food_or_recipe(string because, (int, PlanEntryKind)[] entries, int days, int[] empty) =>
        DraftFoodCoverage.EmptyDays(entries, days).Should().Equal(empty, because);

    public static TheoryData<int[], int, string[], string> Messages => new()
    {
        { [1], 2, ["Dragonfruit salad"], "No draft was saved: the catalogue has no foods for day 2 of the plan. Not in the catalogue: Dragonfruit salad. Add those foods, or ask an administrator to import the starter foods, then try again." },
        { [0, 2, 3], 7, ["Kitfo", "Ayib"], "No draft was saved: the catalogue has no foods for days 1, 3 and 4 of the plan. Not in the catalogue: Kitfo and Ayib. Add those foods, or ask an administrator to import the starter foods, then try again." },
        { [0, 1], 2, [], "No draft was saved: the catalogue has no foods for any day of the plan. Add those foods, or ask an administrator to import the starter foods, then try again." },
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public void Says_which_days_and_foods(int[] empty, int days, string[] missing, string message) =>
        DraftFoodCoverage.Message(empty, days, missing).Should().Be(message);
}
