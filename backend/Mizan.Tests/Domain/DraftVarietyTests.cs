using FluentAssertions;
using Mizan.Domain.Planning;
using Xunit;

namespace Mizan.Tests.PlanningRules;

public class DraftVarietyTests
{
    private static readonly Guid Oats = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid Lentils = Guid.Parse("00000000-0000-0000-0000-00000000000b");

    private static DraftVariety.Item[] Every(string slot, Guid id, string name, params int[] days) =>
        days.Select(d => new DraftVariety.Item(d, slot, id, name)).ToArray();

    public static TheoryData<string, DraftVariety.Item[], int, bool, string[]> Cases => new()
    {
        { "a week with oats on three breakfasts is varied enough", Every("breakfast", Oats, "Oats", 0, 2, 4), 7, false, [] },
        { "oats on four of seven breakfasts repeats", Every("breakfast", Oats, "Oats", 0, 1, 2, 3), 7, false,
            ["Oats is at breakfast on 4 of 7 days; use it on at most 3 and vary the rest."] },
        { "batch cooking may eat a batch on half the days", Every("lunch", Lentils, "Lentils", 0, 1, 2, 3), 7, true, [] },
        { "but not on five", Every("lunch", Lentils, "Lentils", 0, 1, 2, 3, 4), 7, true,
            ["Lentils is at lunch on 5 of 7 days; use it on at most 4 and vary the rest."] },
        { "the same food at different meals is not a repeat",
            [.. Every("breakfast", Oats, "Oats", 0, 1), .. Every("dinner", Oats, "Oats", 2, 3)], 7, false, [] },
        { "two lines of it on one day count as one day",
            [.. Every("lunch", Lentils, "Lentils", 0, 0, 1)], 3, false, [] },
        { "a plan shorter than three days is not checked", Every("dinner", Lentils, "Lentils", 0, 1), 2, false, [] },
        { "a short plan still allows two", Every("dinner", Lentils, "Lentils", 0, 1, 2), 3, false,
            ["Lentils is at dinner on 3 of 3 days; use it on at most 2 and vary the rest."] },
        { "the most repeated comes first",
            [.. Every("breakfast", Oats, "Oats", 0, 1, 2, 3), .. Every("dinner", Lentils, "Lentils", 0, 1, 2, 3, 4, 5)], 7, false,
            ["Lentils is at dinner on 6 of 7 days; use it on at most 3 and vary the rest.",
             "Oats is at breakfast on 4 of 7 days; use it on at most 3 and vary the rest."] },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Finds_a_food_or_recipe_served_at_one_meal_too_often(
        string because, DraftVariety.Item[] items, int days, bool batch, string[] messages) =>
        DraftVariety.Repeats(items, days, batch).Select(r => r.Message).Should().Equal(messages, because);
}
