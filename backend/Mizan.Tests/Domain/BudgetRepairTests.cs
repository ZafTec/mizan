using FluentAssertions;
using Mizan.Domain.Planning;
using Xunit;
using static Mizan.Domain.Planning.BudgetRepair;

namespace Mizan.Tests.PlanningRules;

public class BudgetRepairTests
{
    private static readonly Guid TilapiaId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid SoyId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid LentilsId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid StewId = Guid.Parse("00000000-0000-0000-0000-000000000004");
    private static readonly Guid MisirId = Guid.Parse("00000000-0000-0000-0000-000000000005");

    // Per gram: tilapia ETB 0.90, soy chunks ETB 0.06 (both mostly protein), lentils ETB 0.015 (mostly carbs).
    private static readonly Option Tilapia = new(ItemKind.Food, TilapiaId, "Tilapia", new(0.96m, 0.20m, 0m, 0.017m, 0m), 0.90m);
    private static readonly Option Soy = new(ItemKind.Food, SoyId, "Soy chunks", new(3.45m, 0.52m, 0.33m, 0.005m, 0m), 0.06m);
    private static readonly Option Lentils = new(ItemKind.Food, LentilsId, "Lentils", new(1.16m, 0.09m, 0.20m, 0.004m, 0m), 0.015m);
    // Per serving.
    private static readonly Option Misir = new(ItemKind.Recipe, MisirId, "Misir wot", new(600m, 30m, 80m, 15m, 10m), 40m);

    /// <summary>300 g of tilapia on day 0: 288 kcal, 60 g protein, ETB 270.</summary>
    private static Line TilapiaLine(int index = 0, int day = 0, decimal cost = 270m) =>
        new(index, day, ItemKind.Food, TilapiaId, "Tilapia", 300m, new(288m, 60m, 0m, 5.1m, 0m), cost);

    private static readonly IReadOnlyDictionary<int, EntryNutrition> OneDay =
        new Dictionary<int, EntryNutrition> { [0] = new(2100m, 160m, 200m, 60m, 0m), [1] = new(2100m, 160m, 200m, 60m, 0m) };

    private static readonly Targets HighProtein = new(2100m, 158m, null);

    public static TheoryData<string, Line[], Option[], Targets, decimal, decimal, Change[]> Cases => new()
    {
        {
            "the costliest line is swapped for a cheaper food of the same role at the same calories",
            [TilapiaLine()], [Tilapia, Soy, Lentils], HighProtein, 1000m, 800m,
            [new Change(0, ItemKind.Food, SoyId, "Soy chunks", 85m, "Tilapia", 270m - 85m * 0.06m)]
        },
        {
            "a cheaper food of another role is not a swap, so the portion is cut while protein holds",
            [TilapiaLine()], [Tilapia, Lentils], HighProtein, 1000m, 800m,
            [new Change(0, ItemKind.Food, TilapiaId, "Tilapia", 215m, "Tilapia", 270m * (1 - 215m / 300m))]
        },
        {
            "without a calorie target no portion is cut",
            [TilapiaLine()], [Tilapia, Lentils], new Targets(null, 158m, null), 1000m, 800m,
            []
        },
        {
            "a plan within its budget is left alone",
            [TilapiaLine()], [Tilapia, Soy], HighProtein, 700m, 800m,
            []
        },
        {
            "the costliest goes first and the repair stops once the plan is within budget",
            [TilapiaLine(index: 0, day: 1, cost: 100m), TilapiaLine(index: 1, day: 0, cost: 270m)], [Soy], HighProtein, 850m, 800m,
            [new Change(1, ItemKind.Food, SoyId, "Soy chunks", 85m, "Tilapia", 270m - 85m * 0.06m)]
        },
        {
            "a swap that would leave the day short of protein is refused, and the cut stops where protein would",
            [TilapiaLine()], [Soy], new Targets(2100m, 175m, null), 1000m, 800m,
            [new Change(0, ItemKind.Food, TilapiaId, "Tilapia", 290m, "Tilapia", 270m * (1 - 290m / 300m))]
        },
        {
            "a recipe is replaced by a cheaper saved recipe at the same calories",
            [new Line(0, 0, ItemKind.Recipe, StewId, "Beef stew", 1m, new(600m, 45m, 30m, 30m, 0m), 300m)], [Misir, Soy], HighProtein, 1000m, 800m,
            [new Change(0, ItemKind.Recipe, MisirId, "Misir wot", 1m, "Beef stew", 260m)]
        },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheBudgetRepair(string story, Line[] lines, Option[] options, Targets targets, decimal planCost, decimal planBudget, Change[] expected)
    {
        var changes = Repair(lines, OneDay, options, targets, planCost, planBudget);
        changes.Should().BeEquivalentTo(expected, opts => opts.WithStrictOrdering(), story);
    }

    [Theory]
    // Calories at the cheapest kcal (soy, ETB 0.06 for 3.45 kcal) set the floor: 1,890 kcal (a tenth under) a day.
    [InlineData(2100d, 158d, 1890d * 0.06d / 3.45d, "the 2100 kcal target")]
    // Protein alone: 158 g at the cheapest protein (soy, ETB 0.06 for 0.52 g).
    [InlineData(null, 158d, 158d * 0.06d / 0.52d, "the 158 g protein target")]
    public void TheCheapestWayToMeetTheTargets_IsTheFloorOnTheCost(double? kcal, double protein, double daily, string basis)
    {
        var bound = LowerBound([Tilapia, Soy], new Targets((decimal?)kcal, (decimal)protein, null));
        bound.Should().NotBeNull();
        bound!.Value.DailyCost.Should().BeApproximately((decimal)daily, 0.01m);
        bound.Value.Basis.Should().Be(basis);
    }

    [Fact]
    public void NoTarget_IsNoFloor() => LowerBound([Soy], new Targets(null, null, null)).Should().BeNull();

    [Theory]
    [InlineData(2500d, "Meeting the 158 g protein target with the priced foods and your recipes costs at least ETB 2,500.00 a week. Most of the cost: Tilapia (ETB 1,208.20 a week), Soy chunks (ETB 871.10 a week).")]
    [InlineData(900d, "Most of the cost: Tilapia (ETB 1,208.20 a week), Soy chunks (ETB 871.10 a week).")]
    [InlineData(null, "Most of the cost: Tilapia (ETB 1,208.20 a week), Soy chunks (ETB 871.10 a week).")]
    public void AnUnmetBudget_SaysWhy(double? floor, string expected)
    {
        var drivers = CostDrivers([("Soy chunks", 871.10m), ("Onion", 40m), ("Tilapia", 1208.20m)], "ETB");
        DraftSummary.BudgetReason((decimal?)floor, "the 158 g protein target", 1500m, drivers, "ETB").Should().Be(expected);
    }

    [Fact]
    public void TheBudgetLine_CarriesTheReason_AndTheNoteCountsTheLinesCodeChanged()
    {
        var summary = DraftSummary.Describe(
            [new PlanViolation(null, null, PlanRuleNames.BudgetOver, "over")],
            3120.81m, 1500m, "ETB", 0, [], budgetReason: "Most of the cost: Tilapia (ETB 1,208.20 a week).", budgetChanges: 2);

        summary.Unmet.Should().Equal("Budget not met: ETB 3,120.81 a week against ETB 1,500.00. Most of the cost: Tilapia (ETB 1,208.20 a week).");
        summary.Note.Should().Be("1 rule is not met; the draft is saved for review. 2 costly lines were swapped or cut to lower the cost.");
    }
}
