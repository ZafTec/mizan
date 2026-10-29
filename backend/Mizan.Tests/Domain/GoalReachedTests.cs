using FluentAssertions;
using Mizan.Domain.Goals;
using Xunit;

namespace Mizan.Tests.GoalRules;

public class GoalReachedTests
{
    [Theory]
    [InlineData(72, 75, 72.5, false)]  // heading down, not there yet
    [InlineData(72, 72.5, 72, true)]   // on the target counts
    [InlineData(72, 72.5, 71.8, true)] // past it
    [InlineData(80, 78, 80.2, true)]   // heading up
    [InlineData(80, 78, 79.9, false)]
    [InlineData(72, 72, 71, false)]    // already on the target before
    [InlineData(72, 71, 70, false)]    // already past it and moving away
    public void ATargetIsReached_WhenTheValueCrossesIt(double target, double before, double now, bool reached) =>
        GoalReached.Crossed((decimal)target, (decimal)before, (decimal)now).Should().Be(reached);

    [Fact]
    public void WithoutAnEarlierValue_NothingIsReached() =>
        GoalReached.Crossed(72m, null, 71m).Should().BeFalse();

    [Fact]
    public void EachMetricIsCheckedOnItsOwn() =>
        GoalReached.Crossed(new BodyTargets(72m, 18m, 35m), new BodyReading(73m, 19m, 34m), new BodyReading(71.9m, 19.5m, 35.1m))
            .Should().Equal(GoalMetric.Weight, GoalMetric.MuscleMass);

    [Theory]
    [InlineData(176, "lb", 79.832)]
    [InlineData(72, "kg", 72)]
    [InlineData(72, null, 72)]
    public void ATargetWeightIsComparedInKilograms(double target, string? unit, double kilograms) =>
        Math.Round(GoalReached.TargetWeightKg((decimal)target, unit)!.Value, 3).Should().Be((decimal)kilograms);

    [Theory]
    [InlineData(71.84, "kg", "71.8 kg")]
    [InlineData(72, "kg", "72 kg")]
    [InlineData(80, "lb", "176.4 lb")]
    public void AWeightIsShownInTheGoalsUnit(double kilograms, string unit, string shown) =>
        GoalReached.Weight((decimal)kilograms, unit).Should().Be(shown);
}
