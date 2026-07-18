namespace Mizan.Domain.Entities;

public class WorkoutDraft
{
    public Guid UserId { get; set; }
    public string Payload { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
