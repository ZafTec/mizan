namespace Mizan.Domain.Entities;

public class Achievement
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public int Points { get; set; }
    public string? Category { get; set; } // nutrition, workout, streak, milestone

    // Criteria for auto-unlock. CriteriaType is a well-known string handled by IAchievementEvaluator.
    // Known values: "meals_logged", "recipes_created", "workouts_logged", "body_measurements_logged",
    //               "goal_progress_logged", "streak_nutrition", "streak_workout", "points_total".
    public string? CriteriaType { get; set; }

    // Inclusive lower bound for unlock (e.g. 7 for a 7-day streak, 100 for 100 meals logged).
    public int Threshold { get; set; }

    // Navigation properties
    public virtual ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
}
