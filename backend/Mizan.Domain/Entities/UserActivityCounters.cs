namespace Mizan.Domain.Entities;

/// <summary>
/// Running totals of the things achievements count.
///
/// These were <c>COUNT(*)</c> over the user's whole history, run on every
/// single log to check a threshold. That cost grows with the user forever, on
/// the one path that must stay fast. A counter row is O(1) and stays O(1).
///
/// Deletes decrement, because the counts they replace did.
/// </summary>
public class UserActivityCounters
{
    public Guid UserId { get; set; }
    public int MealsLogged { get; set; }
    public int RecipesCreated { get; set; }
    public int WorkoutsLogged { get; set; }
    public int BodyMeasurementsLogged { get; set; }
    public int GoalProgressLogged { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
