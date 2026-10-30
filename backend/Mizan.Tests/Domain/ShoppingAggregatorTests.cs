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
    public void AnItemUsedInSeveralPrepSessions_IsOneLineForThePlan()
    {
        var rice = Food("Rice");
        var berbere = Food("Berbere");
        var stew = Recipe("Stew", 1, Line(rice, 100, "g"), Line(berbere, 10, "g"));
        var sunday = Guid.NewGuid();
        var wednesday = Guid.NewGuid();

        var items = ShoppingAggregator.Aggregate(
            [
                RecipeEntry(stew, 1, day: 0, session: sunday),
                RecipeEntry(stew, 1, day: 1, session: sunday),
                RecipeEntry(stew, 1, day: 3, session: wednesday),
                FoodEntry(rice, 50, day: 5),
            ],
            Recipes(stew), Foods(rice, berbere));

        items.Select(i => (i.Name, i.Grams, i.PrepSessionId, i.Sources)).Should().BeEquivalentTo(
            [("Berbere", (decimal?)30m, (Guid?)null, 3), ("Rice", (decimal?)350m, (Guid?)null, 4)]);
    }

    [Fact]
    public void AnItemOfOnePrepSession_IsListedUnderIt_AndItsOtherUsesAreAnUngroupedLine()
    {
        var rice = Food("Rice");
        var coriander = Food("Coriander");
        var stew = Recipe("Stew", 1, Line(rice, 100, "g"), Line(coriander, 5, "g"));
        var sunday = Guid.NewGuid();

        var items = ShoppingAggregator.Aggregate(
            [RecipeEntry(stew, 1, day: 0, session: sunday), RecipeEntry(stew, 1, day: 1, session: sunday), FoodEntry(rice, 50, day: 5)],
            Recipes(stew), Foods(rice, coriander));

        items.Select(i => (i.Name, i.Grams, i.PrepSessionId, i.Sources)).Should().Equal(
            ("Coriander", (decimal?)10m, (Guid?)sunday, 2), ("Rice", (decimal?)200m, (Guid?)sunday, 2), ("Rice", (decimal?)50m, (Guid?)null, 1));
    }

    [Fact]
    public void ATypedLineNamedLikeAFoodOnTheList_JoinsThatFood()
    {
        var berbere = Food("Berbere");
        var stew = Recipe("Stew", 1, Line(berbere, 20, "g"));
        var shiro = Recipe("Shiro", 1, Line(null, 1, "tbsp", "berbere"));

        var items = ShoppingAggregator.Aggregate([RecipeEntry(stew, 1), RecipeEntry(shiro, 2)], Recipes(stew, shiro), Foods(berbere));

        items.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new ShoppingDraftItem(berbere.Id, "Berbere", 20, 2, "tbsp", true, null, 2),
            "the weighed part and the spoonful cannot be added, so both are shown for review");
    }

    [Fact]
    public void ATypedLineInAPrepSession_JoinsTheFoodsLineOfThatSession()
    {
        var berbere = Food("Berbere");
        var stew = Recipe("Stew", 1, Line(berbere, 20, "g"));
        var shiro = Recipe("Shiro", 1, Line(null, 1, "tbsp", "berbere"));
        var sunday = Guid.NewGuid();

        var items = ShoppingAggregator.Aggregate(
            [RecipeEntry(stew, 1, day: 0, session: sunday), RecipeEntry(shiro, 1, day: 1, session: sunday), RecipeEntry(stew, 1, day: 4)],
            Recipes(stew, shiro), Foods(berbere));

        items.Select(i => (i.Name, i.Grams, i.Amount, i.PrepSessionId)).Should().Equal(
            ("Berbere", (decimal?)20m, (decimal?)1m, (Guid?)sunday), ("Berbere", (decimal?)20m, (decimal?)null, (Guid?)null));
    }

    [Theory]
    [InlineData(2, "tbsp", 1, "tablespoon", 3, "tbsp")]
    [InlineData(1, "cup", 4, "tbsp", 1.25, "cup")]
    [InlineData(1, "tbsp", 3, "tsp", 2, "tbsp")]
    [InlineData(2, "piece", 3, "pcs", 5, "piece")]
    [InlineData(2, "cloves", 1, "clove", 3, "cloves")]
    [InlineData(500, "ml", 1, "l", 1500, "ml")]
    public void LooseAmountsInUnitsThatAgree_AreAdded(double first, string? firstUnit, double second, string? secondUnit, double total, string? unit)
    {
        var tomato = Food("Tomato");
        var a = Recipe("A", 1, Line(tomato, (decimal)first, firstUnit));
        var b = Recipe("B", 1, Line(tomato, (decimal)second, secondUnit));

        var items = ShoppingAggregator.Aggregate([RecipeEntry(a, 1), RecipeEntry(b, 1)], Recipes(a, b), Foods(tomato));

        items.Should().ContainSingle();
        (items[0].Amount, items[0].Unit).Should().Be(((decimal)total, unit));
    }

    [Theory]
    [InlineData(1, "tbsp", "tbsp", 1.0)]
    [InlineData(1, "Tablespoons", "tbsp", 1.0)]
    [InlineData(3, "tsp", "tbsp", 1.0)]
    [InlineData(8, "tbsp", "cup", 0.5)]
    [InlineData(1.2, "l", "cup", 5.0)]
    [InlineData(2, "pcs", "piece", 2.0)]
    [InlineData(1, "handful", "cup", null)]
    [InlineData(1, "bunch", "piece", null)]
    public void ConvertLoose_ConvertsSpellingsAndVolumes_AndNothingElse(double amount, string? from, string? to, double? expected)
    {
        ShoppingAggregator.ConvertLoose((decimal)amount, from, to).Should().Be(expected is null ? null : (decimal)expected);
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
