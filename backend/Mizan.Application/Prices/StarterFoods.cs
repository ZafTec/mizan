using System.Security.Cryptography;
using System.Text;

namespace Mizan.Application.Prices;

/// <summary>One starter food, per 100 g as eaten or as bought, and where its numbers come from.</summary>
public sealed record StarterFood(string Name, decimal Kcal, decimal Protein, decimal Carbs, decimal Fat, decimal? Fiber, string Source)
{
    public bool IsSourced => Source == StarterFoods.UsdaSrLegacy;
}

/// <summary>
/// The shared catalogue a fresh deployment starts from, so planning, costing and the price
/// review have foods to name (docs/PLANNING.md#prices). Generic foods carry USDA SR Legacy values
/// per 100 g and are marked verified. Ethiopian dishes and blends have no USDA entry: their
/// values are estimates from typical recipes, stored unverified so an administrator can correct
/// them. Names match <see cref="AddisAbabaStaplePrices"/>, so the staple prices attach to them.
/// Ids are derived from the name exactly as <c>CatalogSeed</c> derives exercise ids, so an
/// import is repeatable and a seed written in SQL would agree.
/// </summary>
public static class StarterFoods
{
    public const string UsdaSrLegacy = "USDA SR Legacy";
    public const string Estimate = "Estimate";

    /// <summary><c>md5('mizan-food:' || name)::uuid</c>, as PostgreSQL computes it.</summary>
    public static Guid IdFor(string name) =>
        Guid.ParseExact(Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes("mizan-food:" + name))), "N");

    public static readonly IReadOnlyList<StarterFood> All =
    [
        // Grains and pulses, dry.
        new("Teff", 367m, 13.3m, 73.1m, 2.4m, 8.0m, UsdaSrLegacy),
        new("Wheat flour", 364m, 10.3m, 76.3m, 1.0m, 2.7m, UsdaSrLegacy),
        new("Rice", 365m, 7.1m, 80.0m, 0.7m, 1.3m, UsdaSrLegacy),
        new("Pasta", 371m, 13.0m, 74.7m, 1.5m, 3.2m, UsdaSrLegacy),
        new("Lentils", 352m, 24.6m, 63.4m, 1.1m, 10.7m, UsdaSrLegacy),
        new("Chickpeas", 378m, 20.5m, 62.9m, 6.0m, 12.2m, UsdaSrLegacy),
        new("Broad beans", 341m, 26.1m, 58.3m, 1.5m, 25.0m, UsdaSrLegacy),

        // Vegetables and fruit, raw.
        new("Onion", 40m, 1.1m, 9.3m, 0.1m, 1.7m, UsdaSrLegacy),
        new("Tomato", 18m, 0.9m, 3.9m, 0.2m, 1.2m, UsdaSrLegacy),
        new("Potato", 77m, 2.0m, 17.5m, 0.1m, 2.1m, UsdaSrLegacy),
        new("Carrot", 41m, 0.9m, 9.6m, 0.2m, 2.8m, UsdaSrLegacy),
        new("Cabbage", 25m, 1.3m, 5.8m, 0.1m, 2.5m, UsdaSrLegacy),
        new("Kale", 49m, 4.3m, 8.8m, 0.9m, 3.6m, UsdaSrLegacy),
        new("Collard greens", 32m, 3.0m, 5.4m, 0.6m, 4.0m, UsdaSrLegacy),
        new("Garlic", 149m, 6.4m, 33.1m, 0.5m, 2.1m, UsdaSrLegacy),
        new("Ginger", 80m, 1.8m, 17.8m, 0.8m, 2.0m, UsdaSrLegacy),
        new("Banana", 89m, 1.1m, 22.8m, 0.3m, 2.6m, UsdaSrLegacy),
        new("Orange", 47m, 0.9m, 11.8m, 0.1m, 2.4m, UsdaSrLegacy),
        new("Avocado", 160m, 2.0m, 8.5m, 14.7m, 6.7m, UsdaSrLegacy),

        // Meat, fish, eggs and dairy, raw.
        new("Eggs", 143m, 12.6m, 0.7m, 9.5m, null, UsdaSrLegacy),
        new("Chicken breast", 120m, 22.5m, 0m, 2.6m, null, UsdaSrLegacy),
        new("Beef mince", 215m, 18.6m, 0m, 15.0m, null, UsdaSrLegacy),
        new("Lamb", 282m, 16.6m, 0m, 23.4m, null, UsdaSrLegacy),
        new("Tilapia", 96m, 20.1m, 0m, 1.7m, null, UsdaSrLegacy),
        new("Whole milk", 61m, 3.2m, 4.8m, 3.3m, null, UsdaSrLegacy),
        new("Yoghurt", 61m, 3.5m, 4.7m, 3.3m, null, UsdaSrLegacy),
        new("Butter", 717m, 0.9m, 0.1m, 81.1m, null, UsdaSrLegacy),
        new("Sunflower oil", 884m, 0m, 0m, 100m, null, UsdaSrLegacy),
        new("Sugar", 387m, 0m, 100m, 0m, null, UsdaSrLegacy),

        // Ethiopian staples: estimates from typical recipes.
        new("Injera", 165m, 5.0m, 33.0m, 1.0m, 3.0m, Estimate),
        new("Berbere", 290m, 12.0m, 50.0m, 10.0m, 30.0m, Estimate),
        new("Niter kibbeh", 870m, 0.3m, 0.5m, 98.0m, null, Estimate),
        new("Ayib", 100m, 11.0m, 3.5m, 4.5m, null, Estimate),
        new("Shiro powder", 360m, 20.0m, 55.0m, 6.0m, 12.0m, Estimate),
        new("Shiro wat", 130m, 6.0m, 14.0m, 6.0m, 4.0m, Estimate),
        new("Misir wat", 120m, 7.0m, 15.0m, 4.0m, 4.0m, Estimate),
        new("Gomen", 70m, 3.0m, 6.0m, 4.0m, 3.0m, Estimate),
        new("Doro wat", 150m, 12.0m, 5.0m, 9.0m, 1.0m, Estimate),
        new("Kitfo", 250m, 18.0m, 0.5m, 20.0m, null, Estimate),
        new("Tibs", 210m, 22.0m, 3.0m, 12.0m, 0.5m, Estimate),
        new("Kolo", 370m, 11.0m, 75.0m, 2.5m, 10.0m, Estimate),
    ];
}
