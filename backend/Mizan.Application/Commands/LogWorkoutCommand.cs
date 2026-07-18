using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record LogWorkoutCommand : IRequest<LogWorkoutResult>
{
    public string? Name { get; init; }
    public DateOnly WorkoutDate { get; init; }
    public Guid? TemplateId { get; init; }
    public decimal? BodyweightKg { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int? DurationMinutes { get; init; }
    public int? CaloriesBurned { get; init; }
    public string? Notes { get; init; }
    public List<WorkoutExerciseDto> Exercises { get; init; } = new();
}

public record WorkoutExerciseDto
{
    public Guid ExerciseId { get; init; }
    public string? Notes { get; init; }
    public bool SupersetWithNext { get; init; }
    public List<ExerciseSetDto> Sets { get; init; } = new();
}

public record ExerciseSetDto
{
    public int? Reps { get; init; }
    public decimal? WeightKg { get; init; }
    public int? DurationSeconds { get; init; }
    public decimal? DistanceMeters { get; init; }
    public decimal? ResistanceLevel { get; init; }
    public decimal? InclinePercent { get; init; }
    public int? Steps { get; init; }
    public DateTime? CompletedAt { get; init; }
    public bool Completed { get; init; } = true;
}

public record LogWorkoutResult
{
    public Guid Id { get; init; }
    public string Message { get; init; } = string.Empty;
    public int TotalExercises { get; init; }
    public int TotalSets { get; init; }
    public StreakUpdate? Streak { get; init; }
    public IReadOnlyList<UnlockedAchievement> UnlockedAchievements { get; init; } = [];
}

public class LogWorkoutCommandValidator : AbstractValidator<LogWorkoutCommand>
{
    public LogWorkoutCommandValidator()
    {
        RuleFor(x => x.Name).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.DurationMinutes).InclusiveBetween(1, 1_440).When(x => x.DurationMinutes.HasValue);
        RuleFor(x => x.CaloriesBurned).InclusiveBetween(0, 20_000).When(x => x.CaloriesBurned.HasValue);
        RuleFor(x => x.BodyweightKg).InclusiveBetween(20, 500).When(x => x.BodyweightKg.HasValue);
        RuleFor(x => x.Exercises).NotEmpty().Must(x => x.Count <= 30);
        RuleForEach(x => x.Exercises).ChildRules(exercise =>
        {
            exercise.RuleFor(x => x.ExerciseId).NotEmpty();
            exercise.RuleFor(x => x.Notes).MaximumLength(500);
            exercise.RuleFor(x => x.Sets).NotEmpty().Must(x => x.Count <= 50);
            exercise.RuleForEach(x => x.Sets).ChildRules(set =>
            {
                set.RuleFor(x => x.Reps).InclusiveBetween(0, 1_000).When(x => x.Reps.HasValue);
                set.RuleFor(x => x.WeightKg).InclusiveBetween(0, 1_000).When(x => x.WeightKg.HasValue);
                set.RuleFor(x => x.DurationSeconds).InclusiveBetween(0, 86_400).When(x => x.DurationSeconds.HasValue);
                set.RuleFor(x => x.DistanceMeters).InclusiveBetween(0, 1_000_000).When(x => x.DistanceMeters.HasValue);
                set.RuleFor(x => x.Steps).InclusiveBetween(0, 200_000).When(x => x.Steps.HasValue);
            });
        });
    }
}

public class LogWorkoutCommandHandler : IRequestHandler<LogWorkoutCommand, LogWorkoutResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IStreakService _streakService;
    private readonly IAchievementEvaluator _achievements;

    public LogWorkoutCommandHandler(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        IStreakService streakService,
        IAchievementEvaluator achievements)
    {
        _context = context;
        _currentUser = currentUser;
        _streakService = streakService;
        _achievements = achievements;
    }

    public async Task<LogWorkoutResult> Handle(LogWorkoutCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var exerciseIds = request.Exercises.Select(e => e.ExerciseId).Distinct().ToList();
        var existingExerciseIds = await _context.Exercises
            .Where(e => exerciseIds.Contains(e.Id) && (!e.IsCustom || e.CreatedByUserId == _currentUser.UserId))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
        if (existingExerciseIds.Count != exerciseIds.Count)
        {
            throw new DomainValidationException("One or more exercises are invalid or inaccessible");
        }

        var workout = new Workout
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId.Value,
            Name = request.Name ?? $"Workout on {request.WorkoutDate:MMM dd}",
            WorkoutDate = request.WorkoutDate,
            TemplateId = request.TemplateId,
            BodyweightKg = request.BodyweightKg,
            StartedAt = request.StartedAt,
            CompletedAt = request.CompletedAt ?? DateTime.UtcNow,
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
                SortOrder = i,
                Notes = exerciseDto.Notes,
                SupersetWithNext = exerciseDto.SupersetWithNext
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
                    ResistanceLevel = setDto.ResistanceLevel,
                    InclinePercent = setDto.InclinePercent,
                    Steps = setDto.Steps,
                    CompletedAt = setDto.CompletedAt,
                    Completed = setDto.Completed
                });
                totalSets++;
            }

            workout.Exercises.Add(workoutExercise);
        }

        _context.Workouts.Add(workout);
        if (request.BodyweightKg.HasValue)
        {
            _context.BodyMeasurements.Add(new BodyMeasurement
            {
                Id = Guid.NewGuid(),
                UserId = _currentUser.UserId.Value,
                MeasurementDate = request.WorkoutDate,
                WeightKg = request.BodyweightKg,
                Notes = "Workout bodyweight",
                CreatedAt = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync(cancellationToken);

        var streak = await _streakService.RecordActivityAsync("workout", request.WorkoutDate, cancellationToken);
        var unlocked = await _achievements.EvaluateAsync(cancellationToken);

        return new LogWorkoutResult
        {
            Id = workout.Id,
            Message = $"Logged workout: {workout.Name}",
            TotalExercises = request.Exercises.Count,
            TotalSets = totalSets,
            Streak = streak,
            UnlockedAchievements = unlocked
        };
    }
}
