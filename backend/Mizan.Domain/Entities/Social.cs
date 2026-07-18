namespace Mizan.Domain.Entities;

public class SocialProfile
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public bool DefaultPublishWorkouts { get; set; }
    public string ShareToken { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public virtual User User { get; set; } = null!;
}

public class Follow
{
    public Guid Id { get; set; }
    public Guid FollowerUserId { get; set; }
    public Guid FolloweeUserId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public virtual User Follower { get; set; } = null!;
    public virtual User Followee { get; set; } = null!;
}

public class FeedItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = "WorkoutCompleted";
    public Guid? WorkoutId { get; set; }
    public Guid? AchievementId { get; set; }
    public Guid? TemplateId { get; set; }
    public string? Caption { get; set; }
    public DateTime CreatedAt { get; set; }
    public virtual User User { get; set; } = null!;
    public virtual Workout? Workout { get; set; }
    public virtual Achievement? Achievement { get; set; }
    public virtual WorkoutTemplate? Template { get; set; }
    public virtual ICollection<FeedReaction> Reactions { get; set; } = new List<FeedReaction>();
    public virtual ICollection<FeedComment> Comments { get; set; } = new List<FeedComment>();
}

public class FeedReaction
{
    public Guid Id { get; set; }
    public Guid FeedItemId { get; set; }
    public Guid UserId { get; set; }
    public string Emoji { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public virtual FeedItem FeedItem { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}

public class FeedComment
{
    public Guid Id { get; set; }
    public Guid FeedItemId { get; set; }
    public Guid UserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public virtual FeedItem FeedItem { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}

public class ContentReport
{
    public Guid Id { get; set; }
    public Guid? ReporterUserId { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolutionNote { get; set; }
}
