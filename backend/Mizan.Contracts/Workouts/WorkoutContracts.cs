namespace Mizan.Contracts.Workouts;

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

public record WorkoutExerciseDto
{
    public Guid ExerciseId { get; init; }
    public string? Notes { get; init; }
    public bool SupersetWithNext { get; init; }
    public List<ExerciseSetDto> Sets { get; init; } = new();
}

/// <summary>Body of POST /api/Workouts.</summary>
public record LogWorkoutRequest
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
