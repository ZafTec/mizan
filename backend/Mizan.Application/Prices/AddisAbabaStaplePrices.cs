using Mizan.Domain.Pricing;

namespace Mizan.Application.Prices;

/// <summary>
/// About thirty Addis Ababa staples as <see cref="PriceConfidence.Estimated"/>
/// prices in ETB, for October 2026. They fill the gaps until a scraper or a
/// report gives a better number. numbeo answers 403 to scripts, so these are
/// rounded estimates entered by hand (docs/PLANNING.md#prices); an
/// administrator corrects any of them with a manual price.
/// Each staple lists the catalogue names it may appear under; matching is
/// exact and ignores case.
/// </summary>
public static class AddisAbabaStaplePrices
{
    public const string RegionCode = PriceRegionDefaults.Code;

    public static readonly DateTime ObservedAt = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    public sealed record Staple(string[] Names, decimal Price, decimal Quantity, string Unit);

    public static readonly IReadOnlyList<Staple> All =
    [
        new(["Teff", "Teff flour", "Teff grain", "White teff"], 180m, 1, PriceUnits.Kilogram),
        new(["Wheat flour", "Flour", "All-purpose flour"], 110m, 1, PriceUnits.Kilogram),
        new(["Rice", "White rice"], 160m, 1, PriceUnits.Kilogram),
        new(["Pasta", "Spaghetti", "Macaroni"], 140m, 1, PriceUnits.Kilogram),
        new(["Lentils", "Red lentils", "Misir"], 200m, 1, PriceUnits.Kilogram),
        new(["Chickpeas", "Shimbra"], 150m, 1, PriceUnits.Kilogram),
        new(["Broad beans", "Fava beans", "Bakela"], 130m, 1, PriceUnits.Kilogram),
        new(["Berbere"], 600m, 1, PriceUnits.Kilogram),
        new(["Onion", "Onions", "Red onion", "Red onions"], 70m, 1, PriceUnits.Kilogram),
        new(["Tomato", "Tomatoes"], 70m, 1, PriceUnits.Kilogram),
        new(["Potato", "Potatoes"], 50m, 1, PriceUnits.Kilogram),
        new(["Carrot", "Carrots"], 60m, 1, PriceUnits.Kilogram),
        new(["Cabbage"], 40m, 1, PriceUnits.Kilogram),
        new(["Kale", "Gomen", "Collard greens"], 50m, 1, PriceUnits.Kilogram),
        new(["Garlic"], 250m, 1, PriceUnits.Kilogram),
        new(["Ginger"], 200m, 1, PriceUnits.Kilogram),
        // Sold by the piece at about 18 ETB for a 50 g egg; quoted per kilogram so no serving size is needed.
        new(["Egg", "Eggs"], 360m, 1, PriceUnits.Kilogram),
        new(["Chicken", "Chicken breast", "Whole chicken"], 650m, 1, PriceUnits.Kilogram),
        new(["Beef", "Beef mince", "Minced beef"], 900m, 1, PriceUnits.Kilogram),
        new(["Lamb", "Mutton"], 950m, 1, PriceUnits.Kilogram),
        new(["Tilapia", "Fish"], 450m, 1, PriceUnits.Kilogram),
        new(["Milk", "Whole milk"], 90m, 1, PriceUnits.Litre),
        new(["Yoghurt", "Yogurt", "Ergo"], 160m, 1, PriceUnits.Kilogram),
        new(["Ayib", "Cottage cheese"], 300m, 1, PriceUnits.Kilogram),
        new(["Butter", "Kibe", "Niter kibbeh"], 1200m, 1, PriceUnits.Kilogram),
        new(["Sunflower oil", "Vegetable oil", "Cooking oil"], 300m, 1, PriceUnits.Litre),
        new(["Sugar"], 130m, 1, PriceUnits.Kilogram),
        new(["Salt"], 30m, 1, PriceUnits.Kilogram),
        new(["Coffee", "Coffee beans"], 700m, 1, PriceUnits.Kilogram),
        new(["Banana", "Bananas"], 70m, 1, PriceUnits.Kilogram),
        new(["Orange", "Oranges"], 90m, 1, PriceUnits.Kilogram),
        new(["Avocado", "Avocados"], 80m, 1, PriceUnits.Kilogram),
        // One injera is about 300 g at about 16 ETB.
        new(["Injera"], 55m, 1, PriceUnits.Kilogram),
    ];
}
