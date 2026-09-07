using FluentAssertions;
using Mizan.Application.Commands;
using Mizan.Contracts.Workouts;
using Xunit;

namespace Mizan.Tests.Application;

public class SpineValidationTests
{
    [Fact]
    public void WorkoutEditsCannotBypassSetValidation()
    {
        var exercises = new List<WorkoutExerciseDto>
        {
            new() { ExerciseId = Guid.NewGuid(), Sets = [new ExerciseSetDto { WeightKg = -10, Reps = 8 }] }
        };
        var day = new DateOnly(2026, 9, 2);
        new LogWorkoutCommandValidator().Validate(new LogWorkoutCommand { WorkoutDate = day, Exercises = exercises }).IsValid.Should().BeFalse();
        new UpdateWorkoutCommandValidator().Validate(new UpdateWorkoutCommand { Id = Guid.NewGuid(), WorkoutDate = day, Exercises = exercises }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData(-1d, null, false)]
    [InlineData(70d, 120d, false)]
    [InlineData(70d, null, true)]
    [InlineData(null, 18d, true)]
    public void MeasurementsNeedAtLeastOnePlausibleValue(double? weight, double? bodyFat, bool valid)
    {
        var command = new LogBodyMeasurementCommand(Guid.NewGuid(), DateTime.UtcNow,
            weight.HasValue ? (decimal)weight.Value : null, bodyFat.HasValue ? (decimal)bodyFat.Value : null,
            null, null, null, null, null, null, null, null, null);
        new LogBodyMeasurementCommandValidator().Validate(command).IsValid.Should().Be(valid);
    }
}
