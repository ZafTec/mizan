namespace Mizan.Domain.Entities;

/// <summary>
/// The account. Owned by this backend end to end since v2 - see
/// docs/REFOCUS.md §6. PasswordHash is null for accounts that only ever signed
/// in through Google or GitHub.
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string? Name { get; set; }
    public string? Image { get; set; }

    /// <summary>
    /// IANA zone id, e.g. "Africa/Addis_Ababa". Null means we have never been
    /// told, and the user is treated as UTC until they say otherwise.
    ///
    /// This is what a "day" means for streaks and daily totals. Without it a
    /// user three hours east of UTC has their late-night logs recorded against
    /// the previous day, and their streak never advances.
    /// </summary>
    public string? TimeZoneId { get; set; }
    public string? PasswordHash { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public string ThemePreference { get; set; } = "system";
    public bool CompactMode { get; set; }
    public bool ReduceAnimations { get; set; }
    public string Role { get; set; } = "user";
    public bool Banned { get; set; } = false;
    public string? BanReason { get; set; }
    public DateTime? BanExpires { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Business navigation properties (backend-owned tables)
    public virtual ICollection<HouseholdMember> HouseholdMemberships { get; set; } = new List<HouseholdMember>();
    public virtual ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
    public virtual ICollection<FoodDiaryEntry> FoodDiaryEntries { get; set; } = new List<FoodDiaryEntry>();
    public virtual ICollection<Workout> Workouts { get; set; } = new List<Workout>();
    public virtual ICollection<UserGoal> Goals { get; set; } = new List<UserGoal>();
    public virtual ICollection<UserAchievement> Achievements { get; set; } = new List<UserAchievement>();
    public virtual ICollection<Streak> Streaks { get; set; } = new List<Streak>();
    public virtual ICollection<TrainerClientRelationship> TrainerRelationships { get; set; } = new List<TrainerClientRelationship>();
    public virtual ICollection<TrainerClientRelationship> ClientRelationships { get; set; } = new List<TrainerClientRelationship>();

    public virtual ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
    public virtual ICollection<UserToken> Tokens { get; set; } = new List<UserToken>();
    public virtual ICollection<ExternalLogin> ExternalLogins { get; set; } = new List<ExternalLogin>();
}
