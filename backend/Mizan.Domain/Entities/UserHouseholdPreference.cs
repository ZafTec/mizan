namespace Mizan.Domain.Entities;

// Backend-owned preference table (the users table is frontend-owned via
// Drizzle/BetterAuth, so we store this separately). One row per user holding
// the active household they're currently operating in.
public class UserHouseholdPreference
{
    public Guid UserId { get; set; }
    public Guid? ActiveHouseholdId { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual Household? ActiveHousehold { get; set; }
}
