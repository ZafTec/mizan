namespace Mizan.Domain.Entities;

public class WorkoutTemplate
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ProgramName { get; set; }
    public int SessionOrder { get; set; }
    public string? Notes { get; set; }
    public bool IsBuiltIn { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual User? User { get; set; }
    public virtual ICollection<WorkoutTemplateExercise> Exercises { get; set; } = new List<WorkoutTemplateExercise>();
    public virtual ICollection<Workout> Workouts { get; set; } = new List<Workout>();
}

public class WorkoutTemplateExercise
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public Guid ExerciseId { get; set; }
    public int SortOrder { get; set; }
    public int Sets { get; set; }
    public int? RepsPerSet { get; set; }
    public decimal? TargetWeightKg { get; set; }
    public int? RestSecondsMin { get; set; }
    public int? RestSecondsMax { get; set; }
    public int? RestSecondsFailure { get; set; }
    public bool SupersetWithNext { get; set; }
    public string? Notes { get; set; }
    public string ProgressionType { get; set; } = "None";
    public string ProgressionStrategy { get; set; } = "All";
    public decimal? ProgressionAmountKg { get; set; }
    public string TargetType { get; set; } = "Reps";
    public int? TargetSeconds { get; set; }
    public decimal? TargetDistanceMeters { get; set; }

    public virtual WorkoutTemplate Template { get; set; } = null!;
    public virtual Exercise Exercise { get; set; } = null!;
}
