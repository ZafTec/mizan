namespace Mizan.Domain.Entities;

public class Workout
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? Name { get; set; }
    public DateOnly WorkoutDate { get; set; }
    public int? DurationMinutes { get; set; }
    public int? CaloriesBurned { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual ICollection<WorkoutExercise> Exercises { get; set; } = new List<WorkoutExercise>();
}
