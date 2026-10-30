using FluentAssertions;
using Mizan.Domain.Nutrition;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>The one wording for totals, as the bot, the assistant's tools and its context all read it.</summary>
public class NutritionTextTests
{
    [Theory]
    [InlineData("1850", null, "Calories: 1850 kcal")] // no target
    [InlineData("2000", "2000", "Calories: 2000 / 2000 kcal (100%)")] // target met
    [InlineData("2500", "2000", "Calories: 2500 / 2000 kcal (125%)")] // over target
    [InlineData("1850", "0", "Calories: 1850 kcal")] // zero target is no target, not a division
    [InlineData("1850", "2000", "Calories: 1850 / 2000 kcal (93%)")] // 92.5 reads as 93
    [InlineData("0", "2000", "Calories: 0 / 2000 kcal (0%)")]
    [InlineData("1849.6", "2000", "Calories: 1850 / 2000 kcal (92%)")] // the number rounds for reading only
    public void Describe_PutsATotalAgainstItsTarget(string actual, string? target, string expected)
    {
        var amount = new NutrientAmount(
            "Calories", decimal.Parse(actual), target is null ? null : decimal.Parse(target), "kcal");

        NutritionText.Describe(amount).Should().Be(expected);
    }

    [Fact]
    public void Emphasis_WrapsOnlyTheActualNumber()
    {
        var amount = new NutrientAmount("Protein", 120m, 150m, "g");

        NutritionText.Describe(amount, n => $"<b>{n}</b>").Should().Be("Protein: <b>120</b> / 150 g (80%)");
    }

    [Fact]
    public void Summary_IsOneLine_InTheOrderPeopleReadLabels()
    {
        var text = NutritionText.Summary(NutritionText.Daily(1860m, 120m, 200m, 60m, 2000m, 150m, null, 0m));

        text.Should().Be("Calories: 1860 / 2000 kcal (93%), Protein: 120 / 150 g (80%), Carbs: 200 g, Fat: 60 g");
    }

    [Theory]
    [InlineData("1350", "110", "80", "50", "Left: 650 kcal, 48 g protein, 120 g carbs, 20 g fat")]
    [InlineData("2100", "158", "200", "78", "Left: 0 kcal (100 kcal over), 0 g protein, 0 g carbs, 0 g fat (8 g over)")]
    [InlineData("0", "0", "0", "0", "Left: 2000 kcal, 158 g protein, 200 g carbs, 70 g fat")]
    public void Remaining_IsWhatIsLeftOfEachTarget(string calories, string protein, string carbs, string fat, string expected)
    {
        var amounts = NutritionText.Daily(
            decimal.Parse(calories), decimal.Parse(protein), decimal.Parse(carbs), decimal.Parse(fat),
            2000m, 158m, 200m, 70m);

        NutritionText.Remaining(amounts).Should().Be(expected);
    }

    [Fact]
    public void Remaining_LeavesOutAnAmountWithoutATarget_AndIsEmptyWithNoTargets()
    {
        NutritionText.Remaining(NutritionText.Daily(500m, 30m, 50m, 20m, 2000m, null, 0m, null))
            .Should().Be("Left: 1500 kcal");
        NutritionText.Remaining(NutritionText.Daily(500m, 30m, 50m, 20m)).Should().BeEmpty();
    }
}
