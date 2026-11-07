using FluentAssertions;
using Mizan.Domain.Nutrition;
using Xunit;

namespace Mizan.Tests.NutritionRules;

public class RecipeFitTests
{
    private static readonly DayTargets Targets = new(2000m, 150m, 250m, 70m);

    // Left: 500 kcal, 10 g protein, 150 g carbs, 20 g fat. The carbs are the gap.
    private static readonly Macros Eaten = new(1500m, 140m, 100m, 50m);

    private static readonly (string Title, Macros PerServing)[] Recipes =
    [
        ("Chicken plate", new(450m, 60m, 5m, 10m)),
        ("Rice and lentils", new(480m, 15m, 90m, 8m)),
        ("Butter cake", new(600m, 6m, 70m, 35m)),
    ];

    public static TheoryData<string, DayTargets, Macros, string[]> Cases => new()
    {
        {
            "the recipe that fills the carb gap without going over the protein comes first, the one far over protein last",
            Targets, Eaten, ["Rice and lentils", "Butter cake", "Chicken plate"]
        },
        {
            // 500 kcal left: the chicken plate fills 450 of it; rice 480; the cake goes 100 over, counted twice.
            "with only a calorie target, the closest to what is left without going over",
            new DayTargets(2000m, null, null, null), Eaten, ["Rice and lentils", "Chicken plate", "Butter cake"]
        },
        {
            "a day already over every target ranks the smallest serving first",
            Targets, new Macros(2600m, 200m, 300m, 90m), ["Chicken plate", "Rice and lentils", "Butter cake"]
        },
        {
            "without a target nothing is ranked",
            new DayTargets(null, 0m, null, null), Eaten, []
        },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Rank_OrdersRecipesByHowWellAServingFitsWhatIsLeft(string story, DayTargets targets, Macros eaten, string[] expected)
    {
        var ranked = RecipeFit.Rank(eaten, targets, Recipes, r => r.PerServing);
        ranked.Select(r => r.Item.Title).Should().Equal(expected, story);
    }

    [Fact]
    public void EachRecipe_SaysWhatItWouldLeave()
    {
        var best = RecipeFit.Rank(Eaten, Targets, Recipes, r => r.PerServing)[0];
        best.LeftAfter.Should().Be(new Macros(20m, -5m, 60m, 12m));
        best.Score.Should().Be(0.714m);
    }

    [Fact]
    public void ATie_KeepsTheOrderTheRecipesCameIn()
    {
        var same = new Macros(100m, 5m, 10m, 2m);
        RecipeFit.Rank(Eaten, Targets, [("B", same), ("A", same)], r => r.Item2).Select(r => r.Item.Item1)
            .Should().Equal("B", "A");
    }
}
