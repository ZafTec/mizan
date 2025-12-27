namespace Mizan.Domain.Entities;

public class UserAchievement
{
    public Guid UserId { get; set; }
    public Guid AchievementId { get; set; }
    public DateTime EarnedAt { get; set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual Achievement Achievement { get; set; } = null!;
}
