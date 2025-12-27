namespace Mizan.Domain.Entities;

public class TrainerClientRelationship
{
    public Guid Id { get; set; }
    public Guid TrainerId { get; set; }
    public Guid ClientId { get; set; }
    public string Status { get; set; } = "pending"; // pending, active, paused, ended
    public bool CanViewNutrition { get; set; } = true;
    public bool CanViewWorkouts { get; set; } = true;
    public bool CanViewMeasurements { get; set; }
    public bool CanMessage { get; set; } = true;
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public virtual User Trainer { get; set; } = null!;
    public virtual User Client { get; set; } = null!;
}
