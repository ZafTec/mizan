using FluentAssertions;
using Mizan.Domain.Nutrition;
using Mizan.Domain.Streaks;
using Xunit;

namespace Mizan.Tests.NutritionRules;

public class DiaryDatesTests
{
    private static readonly DateOnly Today = new(2026, 10, 25);

    [Theory]
    [InlineData("2026-10-25", null)]
    [InlineData("2026-10-21", null)]
    [InlineData("2016-10-25", null)]
    [InlineData("2016-10-24", "10 years back")]
    [InlineData("1900-01-01", "10 years back")]
    [InlineData("2027-10-25", null)]
    [InlineData("2027-10-26", "a year ahead")]
    [InlineData("2500-01-01", "a year ahead")]
    public void Problem_RefusesOnlyDatesYearsAway(string date, string? reason)
    {
        var problem = DiaryDates.Problem(DateOnly.Parse(date), Today);

        if (reason is null) problem.Should().BeNull();
        else problem.Should().Contain(reason).And.Contain(date);
    }

    [Fact]
    public void Problem_NamesTheEntry() =>
        DiaryDates.Problem(new DateOnly(2500, 1, 1), Today, DiaryDates.Workout).Should().StartWith("A workout can be logged");

    [Theory]
    [InlineData("2026-10-25", true)]
    [InlineData("2026-10-24", true)]
    [InlineData("2026-10-26", true)]
    [InlineData("2026-10-23", false)]
    [InlineData("2026-10-27", false)]
    [InlineData("1900-01-01", false)]
    public void AStreakCountsOnlyADayWithinOneOfToday(string date, bool counts) =>
        StreakClock.Counts(DateOnly.Parse(date), Today).Should().Be(counts);
}
