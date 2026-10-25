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
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Pick_FindsTheFoodAWrittenNameMeans(string _, string written, string? expected)
    {
        var picked = FoodNameMatch.Pick(written, Catalogue, name => name);
        picked.Should().Be(expected);
    }
}
