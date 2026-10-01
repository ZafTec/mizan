namespace Mizan.Domain.Entities;

/// <summary>
/// A tombstone. A phone that synced yesterday cannot learn from a list that something was removed,
/// because the item is simply absent. This says so, for a limited time.
/// </summary>
public class DeletedRecord
{
    public const string DiaryEntry = "diary_entry";
    public const string Workout = "workout";
    public const string BodyMeasurement = "body_measurement";

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public DateTime DeletedAt { get; set; }
}
