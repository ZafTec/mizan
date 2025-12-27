namespace Mizan.Domain.Entities;

public class GoalProgress
{
    public Guid Id { get; set; }
    public Guid UserGoalId { get; set; }
    public Guid UserId { get; set; }
    public decimal ActualCalories { get; set; }
    public decimal ActualProteinGrams { get; set; }
    public decimal ActualCarbsGrams { get; set; }
    public decimal ActualFatGrams { get; set; }
    public decimal? ActualWeight { get; set; }
    public DateOnly Date { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public virtual UserGoal UserGoal { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
