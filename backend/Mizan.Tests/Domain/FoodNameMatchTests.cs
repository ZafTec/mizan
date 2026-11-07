using FluentAssertions;
using Mizan.Domain.Planning;
using Xunit;

namespace Mizan.Tests.PlanningRules;

public class FoodNameMatchTests
{
    private static readonly string[] Catalogue =
    [
        "Chicken",
        "Chicken breast, raw",
        "Rice",
        "Rice flour",
        "Teff injera",
        "Oil",
        "Egg",
        "Eggplant",
        "Misir wot",
        "Tomatoes",
        "Hummus",
    ];

    public static TheoryData<string, string, string?> Cases => new()
    {
        { "an exact name, ignoring case and spacing", "  chicken ", "Chicken" },
        { "exact beats a longer name that contains it", "Rice", "Rice" },
        { "the closest name that contains the written one", "chicken breast", "Chicken breast, raw" },
        { "a catalogue name inside a longer written one", "grilled chicken breast, raw", "Chicken breast, raw" },
        { "a dish that names a food", "injera with teff injera", "Teff injera" },
        { "nothing that shares a whole name", "Dragonfruit salad", null },
        { "a short catalogue name is not found inside a word", "boiled potatoes in foil", null },
        { "an empty name finds nothing", "   ", null },
        { "a plural names the singular food, not a longer one that starts the same", "eggs", "Egg" },
        { "a singular names the plural food", "tomato", "Tomatoes" },
        { "a common other spelling of a dish", "Misir Wat", "Misir wot" },
        { "a common other name of a food", "aubergine", "Eggplant" },
        { "a word that ends in s is not always a plural", "hummus", "Hummus" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Pick_FindsTheFoodAWrittenNameMeans(string _, string written, string? expected)
    {
        var picked = FoodNameMatch.Pick(written, Catalogue, name => name);
        picked.Should().Be(expected);
    }

    public static TheoryData<string, string, string[]> Keys => new()
    {
        { "a plural is searched by its singular", "Eggs", ["egg"] },
        { "a y plural by the stem both forms contain", "berries", ["berr", "berry"] },
        { "a word with other spellings by each of them", "yoghurt", ["yogurt", "yoghurt", "yogourt"] },
        { "a dish by its longest word", "misir wat", ["misir"] },
        { "nothing long enough is not searched", "a", [] },
    };

    [Theory]
    [MemberData(nameof(Keys))]
    public void SearchKeys_FindEveryNameThatCouldMatch(string _, string written, string[] expected) =>
        FoodNameMatch.SearchKeys(written).Should().Equal(expected);
}
