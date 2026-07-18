using FluentAssertions;
using Mizan.Application.Commands;
using Mizan.Domain;
using Xunit;

namespace Mizan.Tests.Application;

public class WorkoutProgressionTests
{
    [Fact]
    public void IncreaseAllEvenly_AddsAmountToEverySet()
        => WorkoutProgression.Apply([50m, 55m, 60m], "IncreaseAllEvenly", 2.5m).Should().Equal(52.5m, 57.5m, 62.5m);

    [Theory]
    [InlineData("first", 52.5, 50, 60)]
    [InlineData("last", 50, 52.5, 60)]
    [InlineData("all", 52.5, 52.5, 60)]
    public void IncreaseLowestSet_UsesConfiguredStrategy(string strategy, decimal first, decimal second, decimal third)
        => WorkoutProgression.Apply([50m, 50m, 60m], "IncreaseLowestSet", 2.5m, strategy).Should().Equal(first, second, third);

    [Fact]
    public void LogWorkoutValidator_RejectsEmptyWorkout()
    {
        var result = new LogWorkoutCommandValidator().Validate(new LogWorkoutCommand { WorkoutDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Exercises");
    }
}
