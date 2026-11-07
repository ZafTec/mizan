using FluentAssertions;
using Mizan.Domain.Planning;
using Mizan.Domain.Recipes;
using Xunit;
using static Mizan.Tests.PlanningRules.PlanFixtures;

namespace Mizan.Tests.PlanningRules;

public class PlanNutritionTests
{
    [Fact]
    public void ByDay_SumsRecipesByServingsAndFoodsByGrams_AndListsTextAsUnresolved()
    {
        var rice = Food("Rice", kcal: 130, protein: 2.7m, carbs: 28, fat: 0.3m);
        var stew = Recipe("Stew", 4, Line(rice, 400, "g"));
        var perServing = new Dictionary<Guid, RecipeNutritionTotals>
        {
            [stew.Id] = new(200, 15, 20, 8, 3),
        };
        var text = TextEntry("Something from the market", day: 0);
        var entries = new[]
        {
            RecipeEntry(stew, 2, day: 0),
            FoodEntry(rice, 150, day: 0),
            text,
            FoodEntry(rice, 100, day: 2),
        };

        var days = PlanNutrition.ByDay(entries, 3, perServing, Foods(rice));

        days.Should().HaveCount(3);
        days[0].Totals.Should().Be(new EntryNutrition(
            200 * 2 + 130 * 1.5m,
            15 * 2 + 2.7m * 1.5m,
            20 * 2 + 28 * 1.5m,
            8 * 2 + 0.3m * 1.5m,
            3 * 2 + 1 * 1.5m).Round());
        days[0].UnresolvedEntryIds.Should().Equal(text.Id);
        days[1].Totals.Should().Be(EntryNutrition.Zero);
        days[1].UnresolvedEntryIds.Should().BeEmpty();
        days[2].Totals.Calories.Should().Be(130);
    }

    [Fact]
    public void ARecipeWithoutResolvedNutrition_IsUnresolved_NotZero()
    {
        var stew = Recipe("Stew", 4, Line(null, 1, "cup", "something"));
        var entry = RecipeEntry(stew, 1);

        var days = PlanNutrition.ByDay([entry], 1, new Dictionary<Guid, RecipeNutritionTotals>(), new Dictionary<Guid, Mizan.Domain.Entities.Food>());

        days[0].UnresolvedEntryIds.Should().Equal(entry.Id);
        PlanNutrition.Of(entry, new Dictionary<Guid, RecipeNutritionTotals>(), new Dictionary<Guid, Mizan.Domain.Entities.Food>()).Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void AFoodEntryWithoutAPositiveWeight_IsUnresolved(decimal grams)
    {
        var rice = Food("Rice");
        var entry = FoodEntry(rice, grams);

        PlanNutrition.Of(entry, new Dictionary<Guid, RecipeNutritionTotals>(), Foods(rice)).Should().BeNull();
    }

    [Fact]
    public void ARecipeEntryWithoutServings_CountsAsOneServing()
    {
        var stew = Recipe("Stew", 2);
        var entry = RecipeEntry(stew, 1);
        entry.Servings = null;
        var perServing = new Dictionary<Guid, RecipeNutritionTotals> { [stew.Id] = new(300, 20, 30, 10, 5) };

        PlanNutrition.Of(entry, perServing, new Dictionary<Guid, Mizan.Domain.Entities.Food>())!.Value.Calories.Should().Be(300);
    }
}
