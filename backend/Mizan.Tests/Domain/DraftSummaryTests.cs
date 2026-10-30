using FluentAssertions;
using Mizan.Domain.Planning;
using Xunit;

namespace Mizan.Tests.PlanningRules;

public class DraftSummaryTests
{
    [Theory]
    [InlineData(2000d, 2000d, 1d)]      // on target
    [InlineData(2200d, 2000d, 1d)]      // within the tenth the checks allow
    [InlineData(2500d, 2000d, 0.8d)]    // over: scaled to the target
    [InlineData(5000d, 2000d, 0.5d)]    // never below half
    [InlineData(2500d, null, 1d)]      // no target
    public void ADayOverItsCalories_IsScaledToTheTarget_NeverBelowHalf(double calories, double? target, double factor) =>
        PortionScaling.DayFactor((decimal)calories, (decimal?)target).Should().Be((decimal)factor);

    [Theory]
    [InlineData(150d, 0.67d, 100d)]
    [InlineData(4d, 0.5d, 5d)]
    public void ScaledGrams_AreRoundedToFiveGrams(double grams, double factor, double expected) =>
        PortionScaling.Grams((decimal)grams, (decimal)factor).Should().Be((decimal)expected);

    [Theory]
    [InlineData(2d, 0.6d, 1.25d)]
    [InlineData(0.25, 0.5, 0.25)]
    public void ScaledServings_AreRoundedToAQuarter(double servings, double factor, double expected) =>
        PortionScaling.Servings((decimal)servings, (decimal)factor).Should().Be((decimal)expected);

    [Fact]
    public void EachUnmetRule_IsOneLineWithItsAmountAndTarget_AndTheNoteCountsThem()
    {
        var summary = DraftSummary.Describe(
            [
                new PlanViolation(null, null, PlanRuleNames.BudgetOver, "ETB 3,529.00 a week is over the ETB 1,500.00 budget."),
                new PlanViolation(2, "DINNER", PlanRuleNames.CaloriesOver, "2600 kcal is over the 2000 kcal target."),
            ],
            weeklyCost: 3529m, weeklyBudget: 1500m, currency: "ETB", unresolvedLines: 2, scaledDays: [4, 1]);

        summary.Unmet.Should().Equal(
            "Budget not met: ETB 3,529.00 a week against ETB 1,500.00.",
            "Day 3 dinner: 2600 kcal is over the 2000 kcal target.");
        summary.Note.Should().Be("2 rules are not met; the draft is saved for review. "
            + "Portions on days 2, 5 were scaled to the calorie target. "
            + "2 lines are not in the catalogue and are saved as text to review.");
    }

    [Fact]
    public void ADraftThatMeetsEveryRule_SaysSo() =>
        DraftSummary.Describe([], 100m, 1500m, "ETB", 0, []).Should().BeEquivalentTo(
            new DraftSummary.Summary("Meets every rule of the plan.", []));
}
