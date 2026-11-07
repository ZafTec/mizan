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

    public static TheoryData<(decimal? WeightKg, int? Reps)[], decimal> Sessions => new()
    {
        // The retest: 50 kg for 5, 5 and 4 is 58.3 whichever set is read last.
        { [(50m, 5), (50m, 5), (50m, 4)], 58.3m },
        { [(50m, 4), (50m, 5), (50m, 5)], 58.3m },
        { [(50m, 5), (50m, 4), (50m, 5)], 58.3m },
        // A lighter set with more reps can estimate higher than the heaviest one.
        { [(60m, 1), (50m, 10)], 66.7m },
        { [(50m, 10), (60m, 1)], 66.7m },
        // Sets without a weight or reps estimate nothing.
        { [(null, 10), (40m, null)], 40.0m },
        { [], 0m },
    };

    [Theory]
    [MemberData(nameof(Sessions))]
    public void Best_IsTheLargestEstimateInAnyOrder((decimal? WeightKg, int? Reps)[] sets, decimal expected) =>
        OneRepMax.Best(sets).Should().Be(expected);
}
