using FluentAssertions;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;
using Xunit;
using static Mizan.Tests.PlanningRules.PlanFixtures;

namespace Mizan.Tests.PlanningRules;

public class PlanRuleCheckTests
{
    private static readonly Food Bread = Food("Bread", kcal: 250, protein: 8, carbs: 50, fat: 2);
    private static readonly Food Chicken = Food("Chicken", kcal: 165, protein: 31, carbs: 0, fat: 4);

    private static PlanDayNutrition Day(int index, decimal kcal, decimal protein, decimal carbs, params Guid[] unresolved) =>
        new(index, new EntryNutrition(kcal, protein, carbs, 10, 1), unresolved);

    private static readonly PlanEntry BreadEntry = FoodEntry(Bread, 100);
    private static readonly PlanEntry ChickenEntry = FoodEntry(Chicken, 100);

    private static IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> FoodsByEntry => new Dictionary<Guid, IReadOnlyList<Guid>>
    {
        [BreadEntry.Id] = [Bread.Id],
        [ChickenEntry.Id] = [Chicken.Id],
    };

    private static IReadOnlyDictionary<Guid, string> Names => new Dictionary<Guid, string> { [Bread.Id] = "Bread", [Chicken.Id] = "Chicken" };

    public static TheoryData<string, PlanRules, PlanDayNutrition[], decimal?, string[]> Cases => new()
    {
        {
            "no rules, no violations",
            new PlanRules(),
            [Day(0, 2500, 20, 300)],
            null,
            []
        },
        {
            "a forbidden food in an entry",
            new PlanRules { ForbiddenFoodIds = [Bread.Id] },
            [Day(0, 1000, 50, 50)],
            null,
            [PlanRuleNames.ForbiddenFood]
        },
        {
            "an allow list flags every other food",
            new PlanRules { AllowedFoodIds = [Chicken.Id] },
            [Day(0, 1000, 50, 50)],
            null,
            [PlanRuleNames.NotAllowedFood]
        },
        {
            "calories within a tenth are fine",
            new PlanRules { TargetCalories = 2000 },
            [Day(0, 2200, 100, 100)],
            null,
            []
        },
        {
            "calories over by more than a tenth",
            new PlanRules { TargetCalories = 2000 },
            [Day(0, 2201, 100, 100)],
            null,
            [PlanRuleNames.CaloriesOver]
        },
        {
            "protein under by more than a tenth",
            new PlanRules { TargetProtein = 150 },
            [Day(0, 2000, 134, 100)],
            null,
            [PlanRuleNames.ProteinUnder]
        },
        {
            "protein within a tenth is fine",
            new PlanRules { TargetProtein = 150 },
            [Day(0, 2000, 135, 100)],
            null,
            []
        },
        {
            "keto uses a 30 g carb ceiling by default",
            new PlanRules { MacroMode = MacroMode.Keto },
            [Day(0, 2000, 150, 31)],
            null,
            [PlanRuleNames.CarbsOver]
        },
        {
            "keto at the ceiling is fine",
            new PlanRules { MacroMode = MacroMode.Keto },
            [Day(0, 2000, 150, 30)],
            null,
            []
        },
        {
            "an explicit ceiling wins over keto's default",
            new PlanRules { MacroMode = MacroMode.Keto, MaxCarbs = 50 },
            [Day(0, 2000, 150, 45)],
            null,
            []
        },
        {
            "unresolved entries are reported and silence the protein check",
            new PlanRules { TargetProtein = 150 },
            [Day(0, 500, 10, 10, Guid.NewGuid())],
            null,
            [PlanRuleNames.Unresolved]
        },
        {
            "a week over budget",
            new PlanRules { WeeklyBudget = 1000, Currency = "ETB" },
            [Day(0, 1000, 50, 50)],
            1200m,
            [PlanRuleNames.BudgetOver]
        },
        {
            "a week on budget",
            new PlanRules { WeeklyBudget = 1000, Currency = "ETB" },
            [Day(0, 1000, 50, 50)],
            1000m,
            []
        },
        {
            "a daily budget alone needs a day's cost to judge",
            new PlanRules { DailyBudget = 100, Currency = "ETB" },
            [Day(0, 1000, 50, 50)],
            1200m,
            []
        },
        {
            "no cost yet means no budget verdict",
            new PlanRules { WeeklyBudget = 1000, Currency = "ETB" },
            [Day(0, 1000, 50, 50)],
            null,
            []
        },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Evaluate_ReportsEachBrokenRule(string _, PlanRules rules, PlanDayNutrition[] days, decimal? weeklyCost, string[] expectedRules)
    {
        var violations = PlanRuleCheck.Evaluate(rules, [BreadEntry, ChickenEntry], days, FoodsByEntry, Names, weeklyCost);

        violations.Select(v => v.Rule).Should().BeEquivalentTo(expectedRules);
    }

    public static TheoryData<string, decimal?, decimal?[], int[]> DailyBudgetCases => new()
    {
        { "no daily budget, no verdict", null, [500m, 90m], [] },
        { "a day over the daily budget", 100m, [120m, 90m], [0] },
        { "a day on the daily budget is fine", 100m, [100m, 100m], [] },
        { "a day with nothing priced is not judged", 100m, [null, 130m], [1] },
    };

    [Theory]
    [MemberData(nameof(DailyBudgetCases))]
    public void Evaluate_MarksEachDayOverTheDailyBudget(string _, decimal? dailyBudget, decimal?[] costs, int[] overDays)
    {
        var days = costs.Select((_, i) => Day(i, 1000, 50, 50)).ToArray();
        var dayCosts = costs.Select((c, i) => (i, c)).ToDictionary(x => x.i, x => x.c);

        var violations = PlanRuleCheck.Evaluate(
            new PlanRules { DailyBudget = dailyBudget, Currency = "ETB" }, [], days,
            new Dictionary<Guid, IReadOnlyList<Guid>>(), Names, dayCosts: dayCosts);

        violations.Where(v => v.Rule == PlanRuleNames.DailyBudgetOver).Select(v => v.DayIndex).Should().Equal(overDays.Cast<int?>());
    }

    [Fact]
    public void AViolation_NamesTheDayTheSlotAndTheFood()
    {
        var violations = PlanRuleCheck.Evaluate(
            new PlanRules { ForbiddenFoodIds = [Bread.Id] }, [BreadEntry], [Day(0, 250, 8, 50)], FoodsByEntry, Names);

        violations.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new PlanViolation(0, "LUNCH", PlanRuleNames.ForbiddenFood, "Bread is on the forbidden list."));
    }

    [Fact]
    public void ARecipeEntry_IsJudgedByItsIngredientFoods()
    {
        var recipe = Recipe("Sandwich", 1, Line(Bread, 100, "g"), Line(Chicken, 100, "g"));
        var entry = RecipeEntry(recipe, 1, day: 2);
        var byEntry = new Dictionary<Guid, IReadOnlyList<Guid>> { [entry.Id] = [Bread.Id, Chicken.Id] };

        var violations = PlanRuleCheck.Evaluate(
            new PlanRules { ForbiddenFoodIds = [Bread.Id] }, [entry], [Day(2, 415, 39, 50)], byEntry, Names);

        violations.Should().ContainSingle(v => v.Rule == PlanRuleNames.ForbiddenFood && v.DayIndex == 2 && v.Slot == "DINNER");
    }

    [Fact]
    public void DayViolations_AreListedPerDay()
    {
        var violations = PlanRuleCheck.Evaluate(
            new PlanRules { TargetCalories = 1000 },
            [], [Day(0, 1500, 50, 50), Day(1, 900, 50, 50), Day(2, 1200, 50, 50)],
            new Dictionary<Guid, IReadOnlyList<Guid>>(), Names);

        violations.Select(v => v.DayIndex).Should().Equal(0, 2);
    }
}
