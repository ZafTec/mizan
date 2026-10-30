using FluentAssertions;
using Mizan.Domain;
using Mizan.Domain.Goals;
using Xunit;

namespace Mizan.Tests.GoalRules;

public class GoalTypesTests
{
    [Theory]
    [InlineData("weight_loss", true)]
    [InlineData("Muscle_Gain", true)]
    [InlineData(" maintenance ", true)]
    [InlineData("general", true)]
    [InlineData("custom", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsKnown_AcceptsTheListedTypesOnly(string? value, bool known) => GoalTypes.IsKnown(value).Should().Be(known);

    [Fact]
    public void Described_NamesEveryType() => GoalTypes.All.Should().OnlyContain(type => GoalTypes.Described.Contains(type));
}

public class OneRepMaxTests
{
    [Theory]
    [InlineData(60, 3, 66.0)]
    [InlineData(60, 5, 70.0)]
    [InlineData(62.5, 7, 77.1)]
    [InlineData(100, 1, 103.3)]
    [InlineData(80, 0, 80.0)]
    [InlineData(0, 10, 0.0)]
    public void Estimate_IsEpleyToOneDecimal(double weight, int reps, double expected) =>
        OneRepMax.Estimate((decimal)weight, reps).Should().Be((decimal)expected);
}
