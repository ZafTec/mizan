namespace Mizan.Domain.Entities;

public class UserGoal
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? GoalType { get; set; } // weight_loss, muscle_gain, maintenance
    public decimal? TargetCalories { get; set; }
    public decimal? TargetProteinGrams { get; set; }
    public decimal? TargetCarbsGrams { get; set; }
    public decimal? TargetFatGrams { get; set; }
    public decimal? TargetWeight { get; set; }
    public string? WeightUnit { get; set; } = "kg";
    public DateOnly? TargetDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation property
    public virtual User User { get; set; } = null!;
}
