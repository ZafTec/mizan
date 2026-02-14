namespace Mizan.Domain.Entities;

/// <summary>
/// User entity - READ-ONLY for backend (managed by frontend Drizzle ORM)
/// ⚠️ Frontend schema (frontend/db/schema.ts users table) is source of truth
/// ⚠️ Backend CANNOT modify users table - it is excluded from EF Core migrations
/// ⚠️ Backend can only READ user data for authorization and business logic
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string? Name { get; set; }
    public string? Image { get; set; }
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

    // Auth navigation properties REMOVED (managed by frontend)
    // public virtual ICollection<Account> Accounts { get; set; }
    // public virtual ICollection<Session> Sessions { get; set; }
}
