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
    public void PersonalRecords_CountsOnlyWorkoutBestImprovements()
    {
        var exerciseId = Guid.NewGuid();
        var records = new[]
        {
            new WorkoutBestWeight(exerciseId, Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc), 100m),
            new WorkoutBestWeight(exerciseId, Guid.NewGuid(), new DateOnly(2026, 1, 8), new DateTime(2026, 1, 8, 10, 0, 0, DateTimeKind.Utc), 95m),
            new WorkoutBestWeight(exerciseId, Guid.NewGuid(), new DateOnly(2026, 1, 15), new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc), 102.5m)
        };

        PersonalRecords.Count(records).Should().Be(2);
    }

    [Fact]
    public void LogWorkoutValidator_RejectsEmptyWorkout()
    {
        var result = new LogWorkoutCommandValidator().Validate(new LogWorkoutCommand { WorkoutDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Exercises");
    }

    [Fact]
    public void WorkoutTemplateValidator_RejectsUnknownProgressionStrategy()
    {
        var command = new SaveWorkoutTemplateCommand
        {
            Name = "Invalid strategy",
            Exercises =
            [
                new WorkoutTemplateExerciseInput(
                    Guid.NewGuid(), 0, 3, 5, 50m, 60, 120, 180, false,
                    null, "IncreaseLowestSet", "weird", 2.5m, "Reps", null, null)
            ]
        };

        var result = new SaveWorkoutTemplateCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName.EndsWith("ProgressionStrategy"));
    }

    [Fact]
    public void PublishFeedItemValidator_RequiresTypeSpecificReference()
    {
        var result = new PublishFeedItemCommandValidator().Validate(
            new PublishFeedItemCommand("WorkoutCompleted", null, null, null, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "WorkoutId");
    }
}
