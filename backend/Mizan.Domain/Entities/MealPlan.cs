using Mizan.Domain.Planning;

namespace Mizan.Domain.Entities;

/// <summary>
/// A meal plan, or a template for one. Entries carry a day index; a personal
/// plan places day 0 on <see cref="StartDate"/>. See docs/PLANNING.md.
/// </summary>
public class MealPlan
{
    public Guid Id { get; set; }

    /// <summary>The owner. Null only for a global template an administrator published.</summary>
    public Guid? UserId { get; set; }
    public Guid? HouseholdId { get; set; }

    /// <summary>The coach who drafted the plan for the owner. Null when the owner wrote it.</summary>
    public Guid? AuthoredByUserId { get; set; }

    public PlanKind Kind { get; set; } = PlanKind.Personal;
    public PlanStatus Status { get; set; } = PlanStatus.Active;

    public string? Name { get; set; }
    public string? Description { get; set; }

    /// <summary>Day 0 of a personal plan. Null for a template.</summary>
    public DateOnly? StartDate { get; set; }

    public int DurationDays { get; set; } = 7;

    public PlanRules Rules { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public DateOnly? EndDate => StartDate is { } start ? PlanSchedule.EndDate(start, DurationDays) : null;

    public bool Covers(DateOnly date) => StartDate is { } start && PlanSchedule.Covers(start, DurationDays, date);

    // Navigation properties
    public virtual User? User { get; set; }
    public virtual Household? Household { get; set; }
    public virtual User? AuthoredBy { get; set; }
    public virtual ICollection<PlanEntry> Entries { get; set; } = new List<PlanEntry>();
    public virtual ICollection<PrepSession> PrepSessions { get; set; } = new List<PrepSession>();
}
