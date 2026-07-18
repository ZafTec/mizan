namespace Mizan.Domain.Entities;

public class WorkoutExercise
{
    public Guid Id { get; set; }
    public Guid WorkoutId { get; set; }
    public Guid ExerciseId { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
    public bool SupersetWithNext { get; set; }

    // Navigation properties
    public virtual Workout Workout { get; set; } = null!;
    public virtual Exercise Exercise { get; set; } = null!;
    public virtual ICollection<ExerciseSet> Sets { get; set; } = new List<ExerciseSet>();
}
