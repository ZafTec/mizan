namespace Mizan.Domain.Entities;

public class Streak
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string StreakType { get; set; } = string.Empty; // logging, workout, calorie_goal
    public int CurrentCount { get; set; }
    public int LongestCount { get; set; }
    public DateOnly? LastActivityDate { get; set; }

    // Navigation property
    public virtual User User { get; set; } = null!;
}
