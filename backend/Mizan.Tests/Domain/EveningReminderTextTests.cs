using FluentAssertions;
using Mizan.Domain.Planning;
using Xunit;

namespace Mizan.Tests.PlanningRules;

public class EveningReminderTextTests
{
    [Theory]
    [InlineData("20:00", 1640, 1800.0, "Your window closes at 20:00. Today: 1,640 of 1,800 kcal.")]
    [InlineData("20:00", 1640, null, "Your window closes at 20:00. Today: 1,640 kcal.")]
    [InlineData("18:30", 0, 2000.0, "Your window closes at 18:30. Today: 0 of 2,000 kcal.")]
    [InlineData("09:05", 999.5, 1200.4, "Your window closes at 09:05. Today: 1,000 of 1,200 kcal.")]
    [InlineData("00:15", 12345, null, "Your window closes at 00:15. Today: 12,345 kcal.")]
    [InlineData("20:00", -5, null, "Your window closes at 20:00. Today: 0 kcal.")]
    public void TheLine_GivesTheCloseAndTheDaysCaloriesAgainstTheTarget(string end, double calories, double? target, string expected)
    {
        EveningReminderText.Build(TimeOnly.Parse(end), (decimal)calories, (decimal?)target).Should().Be(expected);
    }
}
