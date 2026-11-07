using FluentAssertions;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;
using Mizan.Domain.Pricing;
using Xunit;
using static Mizan.Tests.PlanningRules.PlanFixtures;

namespace Mizan.Tests.PlanningRules;

/// <summary>Which price wins, what a gram costs, and what a plan costs. docs/PLANNING.md#prices.</summary>
public class PriceBookTests
{
    private const string Region = "ET-AA";
    private static readonly DateTime Day = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private static FoodPrice Row(Food food, decimal price, decimal quantity, string unit, PriceConfidence confidence, int daysLater = 0, string region = Region) => new()
    {
        Id = Guid.NewGuid(),
        FoodId = food.Id,
        RegionCode = region,
        Price = price,
        Quantity = quantity,
        Unit = unit,
        Confidence = confidence,
        ObservedAt = Day.AddDays(daysLater),
    };

    // Rows are (price per kg, confidence, days after the first); the expected winner is the row's index.
    public static TheoryData<string, (decimal PerKg, PriceConfidence Confidence, int Days)[], int?> Precedence => new()
    {
        { "the only price", [(100, PriceConfidence.Estimated, 0)], 0 },
        { "observed beats a newer report", [(100, PriceConfidence.Observed, 0), (120, PriceConfidence.Reported, 5)], 0 },
        { "a report beats a newer estimate", [(120, PriceConfidence.Reported, 0), (90, PriceConfidence.Estimated, 9)], 0 },
        { "the newest of one confidence", [(100, PriceConfidence.Observed, 0), (110, PriceConfidence.Observed, 3), (105, PriceConfidence.Observed, 1)], 1 },
        { "the newest estimate when nothing better exists", [(80, PriceConfidence.Estimated, 0), (85, PriceConfidence.Estimated, 2)], 1 },
        { "nothing", [], null },
    };

    [Theory]
    [MemberData(nameof(Precedence))]
    public void PicksTheNewestPriceOfTheHighestConfidence(string because, (decimal PerKg, PriceConfidence Confidence, int Days)[] rows, int? winner)
    {
        var food = Food("Teff");
        var prices = rows.Select(r => Row(food, r.PerKg, 1, "kg", r.Confidence, r.Days)).ToList();

        var quote = PriceBook.Quote(food, Region, "ETB", prices);

        if (winner is null)
        {
            quote.Should().BeNull(because);
            return;
        }
        quote!.PricePerKg.Should().Be(rows[winner.Value].PerKg, because);
        quote.Confidence.Should().Be(rows[winner.Value].Confidence, because);
    }

    [Theory]
    [InlineData(120, 1, "kg", null, 0.12)]
    [InlineData(45, 500, "g", null, 0.09)]
    [InlineData(90, 1, "l", null, 0.09)]
    [InlineData(30, 250, "ml", null, 0.12)]
    [InlineData(18, 1, "piece", 50, 0.36)]
    [InlineData(18, 1, "pc", 50, 0.36)]
    [InlineData(10, 0, "kg", null, null)]
    [InlineData(10, 1, "bunch", null, null)]
    public void PricePerGram(decimal price, decimal quantity, string unit, int? servingSize, double? perGram)
    {
        var food = Food("Egg", servingSize: servingSize ?? 100);

        var result = PriceBook.PerGram(Row(food, price, quantity, unit, PriceConfidence.Observed), food);

        result.Should().Be(perGram is null ? null : (decimal)perGram.Value);
    }

    [Fact]
    public void APriceInAnotherRegion_OrWithAUnitThatDoesNotConvert_IsNeverPicked()
    {
        var food = Food("Rice");
        var prices = new[]
        {
            Row(food, 300, 1, "kg", PriceConfidence.Observed, region: "KE-NB"),
            Row(food, 50, 1, "bunch", PriceConfidence.Observed, daysLater: 3),
            Row(food, 160, 1, "kg", PriceConfidence.Estimated),
        };

        var quote = PriceBook.Quote(food, Region, "ETB", prices);

        quote!.PricePerKg.Should().Be(160);
        quote.Confidence.Should().Be(PriceConfidence.Estimated);
    }

    [Fact]
    public void ALine_IsPricedFromItsGrams_AndAnyLineItCannotPriceIsListedWithTheReason()
    {
        var rice = Food("Rice");
        var salt = Food("Salt");
        var quotes = new Dictionary<Guid, FoodPriceQuote>
        {
            [rice.Id] = new(rice.Id, Region, "ETB", 0.16m, PriceConfidence.Observed, Day, null, null),
        };
        ShoppingDraftItem Line(Guid? food, string name, decimal? grams) => new(food, name, grams, null, null, false, null, 1);

        var cost = PlanCostCalculator.Cost(
            [Line(rice.Id, "Rice", 300), Line(salt.Id, "Salt", 5), Line(rice.Id, "Rice, cups", null), Line(null, "Fruit", null)],
            quotes, "ETB");

        cost.Total.Should().Be(48);
        cost.PricedItems.Should().Be(1);
        cost.Coverage.Should().Be(0.25m);
        cost.LowestConfidence.Should().Be(PriceConfidence.Observed);
        cost.Unpriced.Select(i => i.Reason).Should().Equal(
            "No price for this region yet.", "The amount is not known in grams.", "No catalogue food is linked.");
    }

    [Fact]
    public void APlan_IsCostedPerEntryPerDayAndPerWeek_TheWayItsShoppingListWouldBe()
    {
        var rice = Food("Rice");
        var chicken = Food("Chicken");
        var bowl = Recipe("Bowl", 2, Line(rice, 200, "g"), Line(chicken, 300, "g"));
        var quotes = new Dictionary<Guid, FoodPriceQuote>
        {
            [rice.Id] = new(rice.Id, Region, "ETB", 0.16m, PriceConfidence.Observed, Day, null, null),
            [chicken.Id] = new(chicken.Id, Region, "ETB", 0.65m, PriceConfidence.Estimated, Day, null, null),
        };
        var lunch = RecipeEntry(bowl, 1, day: 0);
        var dinner = RecipeEntry(bowl, 2, day: 2);
        var note = TextEntry("Fruit", day: 2);

        var cost = PlanCosting.Cost([lunch, dinner, note], 14, Recipes(bowl), Foods(rice, chicken), quotes, "ETB");

        cost.EntryCosts[lunch.Id].Should().Be(113.5m);
        cost.EntryCosts[note.Id].Should().BeNull("a note has no price, which is not a price of zero");
        cost.DayCosts[0].Should().Be(113.5m);
        cost.DayCosts[2].Should().Be(227);
        cost.DayCosts.Should().NotContainKey(1);
        cost.TotalCost.Should().Be(340.5m);
        cost.WeeklyCost.Should().Be(170.25m, "a fortnight's cost is halved to compare with a weekly budget");
        cost.Summary.LowestConfidence.Should().Be(PriceConfidence.Estimated);
    }

    [Theory]
    [InlineData(250d, 300d, "ETB", "ETB", false)]
    [InlineData(350d, 300d, "ETB", "etb", true)]
    [InlineData(350d, 300d, "USD", "ETB", null)]
    [InlineData(350d, null, null, "ETB", null)]
    [InlineData(null, 300d, "ETB", "ETB", null)]
    public void OverBudget_ComparesOnlyAmountsInOneCurrency(double? weekly, double? budget, string? budgetCurrency, string currency, bool? over) =>
        PlanCosting.OverBudget((decimal?)weekly, (decimal?)budget, budgetCurrency, currency).Should().Be(over);
}
