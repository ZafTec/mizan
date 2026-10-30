namespace Mizan.Contracts.Workouts;

/// <summary>
/// One exercise of a workout sent by a tool: by its id, or by its name as the catalogue spells it.
/// The server resolves a name to the one exercise it means, or refuses and says which it could be.
/// </summary>
public sealed record NamedWorkoutExercise
{
    public Guid? ExerciseId { get; init; }
    public string? ExerciseName { get; init; }
    public string? Notes { get; init; }
    public bool SupersetWithNext { get; init; }
    public List<ExerciseSetDto> Sets { get; init; } = new();
}

/// <summary>Which exercise an item of the workout was saved against.</summary>
/// <param name="Index">The item's position in the list sent, from 0.</param>
/// <param name="Sent">The name sent, or null when the item gave an id.</param>
public sealed record ResolvedWorkoutExercise(int Index, string? Sent, Guid ExerciseId, string? ExerciseName);
