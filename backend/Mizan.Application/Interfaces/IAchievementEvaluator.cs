namespace Mizan.Application.Interfaces;

public interface IAchievementEvaluator
{
    /// <summary>
    /// Evaluates every un-earned achievement for the current user against their live stats, inserts
    /// UserAchievement rows for any newly-met criteria, and returns the list of unlocks to surface in UI.
    /// Safe to call after any activity (meal, workout, measurement, goal progress, streak tick).
    /// </summary>
    Task<IReadOnlyList<UnlockedAchievement>> EvaluateAsync(CancellationToken cancellationToken = default);
}

public record UnlockedAchievement(
    Guid Id,
    string Name,
    string? Description,
    string? IconUrl,
    int Points,
    string? Category);
