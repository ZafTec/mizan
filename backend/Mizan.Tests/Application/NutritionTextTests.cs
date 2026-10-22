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
}
