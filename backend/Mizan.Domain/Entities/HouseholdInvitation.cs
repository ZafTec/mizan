namespace Mizan.Domain.Entities;

// An invite is always tied to a registered user (InvitedUserId). We resolve
// email → user at invite time; if the email doesn't match anyone yet we reject
// the invite rather than storing a dangling email. Extending to email-only
// invites later is additive (add nullable Email, flip the constraint).
public class HouseholdInvitation
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid InvitedUserId { get; set; }
    public Guid InvitedByUserId { get; set; }
    public string Role { get; set; } = "member";
    public string Status { get; set; } = "pending"; // pending | accepted | declined | revoked | expired
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    public virtual Household Household { get; set; } = null!;
    public virtual User InvitedUser { get; set; } = null!;
    public virtual User InvitedBy { get; set; } = null!;
}
