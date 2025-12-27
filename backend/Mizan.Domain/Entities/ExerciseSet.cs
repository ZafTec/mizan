namespace Mizan.Domain.Entities;

public class ExerciseSet
{
    public Guid Id { get; set; }
    public Guid WorkoutExerciseId { get; set; }
    public int SetNumber { get; set; }
    public int? Reps { get; set; }
    public decimal? WeightKg { get; set; }
    public int? DurationSeconds { get; set; }
    public decimal? DistanceMeters { get; set; }
    public bool Completed { get; set; }

    // Navigation property
    public virtual WorkoutExercise WorkoutExercise { get; set; } = null!;
}
