using FluentValidation;
using MediatR;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record LogWorkoutCommand : IRequest<LogWorkoutResult>
{
    public string? Name { get; init; }
    public DateOnly WorkoutDate { get; init; }
    public int? DurationMinutes { get; init; }
    public int? CaloriesBurned { get; init; }
    public string? Notes { get; init; }
    public List<WorkoutExerciseDto> Exercises { get; init; } = new();
}

public record WorkoutExerciseDto
{
    public Guid ExerciseId { get; init; }
    public List<ExerciseSetDto> Sets { get; init; } = new();
}

public record ExerciseSetDto
{
    public int? Reps { get; init; }
    public decimal? WeightKg { get; init; }
    public int? DurationSeconds { get; init; }
    public decimal? DistanceMeters { get; init; }
}

public record LogWorkoutResult
{
    public Guid Id { get; init; }
    public string Message { get; init; } = string.Empty;
    public int TotalExercises { get; init; }
    public int TotalSets { get; init; }
}

public class LogWorkoutCommandValidator : AbstractValidator<LogWorkoutCommand>
{
    public LogWorkoutCommandValidator()
    {
        RuleFor(x => x.DurationMinutes).GreaterThan(0).When(x => x.DurationMinutes.HasValue);
        RuleFor(x => x.CaloriesBurned).GreaterThanOrEqualTo(0).When(x => x.CaloriesBurned.HasValue);
    }
}

public class LogWorkoutCommandHandler : IRequestHandler<LogWorkoutCommand, LogWorkoutResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public LogWorkoutCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<LogWorkoutResult> Handle(LogWorkoutCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var workout = new Workout
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId.Value,
            Name = request.Name ?? $"Workout on {request.WorkoutDate:MMM dd}",
            WorkoutDate = request.WorkoutDate,
            DurationMinutes = request.DurationMinutes,
            CaloriesBurned = request.CaloriesBurned,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        int totalSets = 0;
        for (int i = 0; i < request.Exercises.Count; i++)
        {
            var exerciseDto = request.Exercises[i];
            var workoutExercise = new WorkoutExercise
            {
                Id = Guid.NewGuid(),
                WorkoutId = workout.Id,
                ExerciseId = exerciseDto.ExerciseId,
                SortOrder = i
            };

            for (int j = 0; j < exerciseDto.Sets.Count; j++)
            {
                var setDto = exerciseDto.Sets[j];
                workoutExercise.Sets.Add(new ExerciseSet
                {
                    Id = Guid.NewGuid(),
                    WorkoutExerciseId = workoutExercise.Id,
                    SetNumber = j + 1,
                    Reps = setDto.Reps,
                    WeightKg = setDto.WeightKg,
                    DurationSeconds = setDto.DurationSeconds,
                    DistanceMeters = setDto.DistanceMeters,
                    Completed = true
                });
                totalSets++;
            }

            workout.Exercises.Add(workoutExercise);
        }

        _context.Workouts.Add(workout);
        await _context.SaveChangesAsync(cancellationToken);

        return new LogWorkoutResult
        {
            Id = workout.Id,
            Message = $"Logged workout: {workout.Name}",
            TotalExercises = request.Exercises.Count,
            TotalSets = totalSets
        };
    }
}
