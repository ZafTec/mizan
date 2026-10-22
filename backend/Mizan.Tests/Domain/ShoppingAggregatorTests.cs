using FluentAssertions;
using Mizan.Domain.Planning;
using Xunit;
using static Mizan.Tests.PlanningRules.PlanFixtures;

namespace Mizan.Tests.PlanningRules;

public class ShoppingAggregatorTests
{
    [Fact]
    public void RecipeLines_AreScaledByServingsOverTheRecipesServings()
    {
        var rice = Food("Rice");
        var stew = Recipe("Stew", 4, Line(rice, 400, "g"));

        var items = ShoppingAggregator.Aggregate([RecipeEntry(stew, 2)], Recipes(stew), Foods(rice));

        items.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new ShoppingDraftItem(rice.Id, "Rice", 200, null, null, false, null, 1));
    }

    [Fact]
    public void OneFoodAcrossRecipesAndFoodEntries_IsOneLineWithTheGramsSummed()
    {
        var onion = Food("Onion");
        var stew = Recipe("Stew", 2, Line(onion, 1, "kg"));
        var salad = Recipe("Salad", 1, Line(onion, 50, "g"));

        var items = ShoppingAggregator.Aggregate(
            [RecipeEntry(stew, 1), RecipeEntry(salad, 3), FoodEntry(onion, 100)],
            Recipes(stew, salad), Foods(onion));

        items.Should().ContainSingle();
        items[0].Grams.Should().Be(500 + 150 + 100);
        items[0].Sources.Should().Be(3);
        items[0].NeedsReview.Should().BeFalse();
    }

    [Fact]
    public void ALineWithAnUnconvertibleUnit_KeepsItsAmountAndUnit_AndNeedsReview()
    {
        var flour = Food("Flour");
        var bread = Recipe("Bread", 2, Line(flour, 3, "cups"));

        var items = ShoppingAggregator.Aggregate([RecipeEntry(bread, 4)], Recipes(bread), Foods(flour));

        items.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new ShoppingDraftItem(flour.Id, "Flour", null, 6, "cups", true, null, 1));
    }

    [Fact]
    public void ALineWithoutALinkedFood_GroupsByItsNormalizedText()
    {
        var stew = Recipe("Stew", 1, Line(null, 1, "bunch", "Fresh  Coriander"));
        var salad = Recipe("Salad", 1, Line(null, 2, "bunch", "fresh coriander"));

        var items = ShoppingAggregator.Aggregate([RecipeEntry(stew, 1), RecipeEntry(salad, 1)], Recipes(stew, salad), Foods());

        items.Should().ContainSingle();
        items[0].FoodId.Should().BeNull();
        items[0].Amount.Should().Be(3);
        items[0].Unit.Should().Be("bunch");
        items[0].NeedsReview.Should().BeTrue();
        items[0].Sources.Should().Be(2);
    }

    [Fact]
    public void UnmeasuredNotes_AreLeftOut()
    {
        var rice = Food("Rice");
        var stew = Recipe("Stew", 1, Line(rice, 100, "g"), Line(null, null, null, "salt to taste"));

        var items = ShoppingAggregator.Aggregate([RecipeEntry(stew, 1)], Recipes(stew), Foods(rice));

        items.Should().ContainSingle(i => i.Name == "Rice");
    }

    [Fact]
    public void ATextEntry_BecomesOneLineForReview()
    {
        var items = ShoppingAggregator.Aggregate([TextEntry("Injera from the shop"), TextEntry("injera from the shop")], Recipes(), Foods());

        items.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new ShoppingDraftItem(null, "Injera from the shop", null, null, null, true, null, 2));
    }

    [Fact]
    public void Lines_AreGroupedByPrepSession_SessionsFirst()
    {
        var rice = Food("Rice");
        var stew = Recipe("Stew", 1, Line(rice, 100, "g"));
        var sunday = Guid.NewGuid();
        var wednesday = Guid.NewGuid();

        var items = ShoppingAggregator.Aggregate(
            [
                RecipeEntry(stew, 1, day: 0, session: sunday),
                RecipeEntry(stew, 1, day: 1, session: sunday),
                RecipeEntry(stew, 1, day: 3, session: wednesday),
                FoodEntry(rice, 50, day: 5),
            ],
            Recipes(stew), Foods(rice));

        items.Should().HaveCount(3);
        items.Where(i => i.PrepSessionId == sunday).Should().ContainSingle().Which.Grams.Should().Be(200);
        items.Where(i => i.PrepSessionId == wednesday).Should().ContainSingle().Which.Grams.Should().Be(100);
        items.Last().PrepSessionId.Should().BeNull("lines without a session come after the sessions");
        items.Last().Grams.Should().Be(50);
    }

    [Fact]
    public void TwoDifferentLooseUnitsForOneItem_KeepTheFirstAndNeedReview()
    {
        var flour = Food("Flour");
        var a = Recipe("A", 1, Line(flour, 2, "cups"));
        var b = Recipe("B", 1, Line(flour, 1, "handful"));

        var items = ShoppingAggregator.Aggregate([RecipeEntry(a, 1), RecipeEntry(b, 1)], Recipes(a, b), Foods(flour));

        items.Should().ContainSingle();
        items[0].Amount.Should().Be(2);
        items[0].Unit.Should().Be("cups");
        items[0].NeedsReview.Should().BeTrue();
    }

    [Theory]
    [InlineData("Red  Onion", "red onion")]
    [InlineData("  Teff ", "teff")]
    [InlineData("Berbere", "berbere")]
    public void NormalizeName_LowersTrimsAndCollapsesSpaces(string input, string expected)
    {
        ShoppingAggregator.NormalizeName(input).Should().Be(expected);
    }
}
