using FluentAssertions;
using Mizan.Domain.Planning;
using Xunit;

namespace Mizan.Tests.PlanningRules;

public class PlanScheduleTests
{
    private static readonly DateOnly Start = new(2026, 10, 12);

    [Theory]
    [InlineData(0, "2026-10-12")]
    [InlineData(6, "2026-10-18")]
    [InlineData(30, "2026-11-11")]
    public void DateOf_AddsTheDayIndexToTheStart(int dayIndex, string expected)
    {
        PlanSchedule.DateOf(Start, dayIndex).Should().Be(DateOnly.Parse(expected));
    }

    [Theory]
    [InlineData(7, "2026-10-18")]
    [InlineData(1, "2026-10-12")]
    [InlineData(0, "2026-10-12")]
    [InlineData(31, "2026-11-11")]
    public void EndDate_IsTheLastDayInsideThePlan(int durationDays, string expected)
    {
        PlanSchedule.EndDate(Start, durationDays).Should().Be(DateOnly.Parse(expected));
    }

    [Theory]
    [InlineData("2026-10-11", 7, null)]
    [InlineData("2026-10-12", 7, 0)]
    [InlineData("2026-10-18", 7, 6)]
    [InlineData("2026-10-19", 7, null)]
    [InlineData("2026-10-12", 1, 0)]
    [InlineData("2026-10-13", 1, null)]
    public void DayIndexOf_IsNullOutsideThePlan(string date, int durationDays, int? expected)
    {
        var day = DateOnly.Parse(date);
        PlanSchedule.DayIndexOf(Start, durationDays, day).Should().Be(expected);
        PlanSchedule.Covers(Start, durationDays, day).Should().Be(expected.HasValue);
    }
}
