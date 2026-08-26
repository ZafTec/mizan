using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain;
using Mizan.Domain.Achievements;

namespace Mizan.Infrastructure.Services;

public class UserStatsProvider : IUserStatsProvider
{
    private readonly IMizanDbContext _context;
    private readonly IActivityCounters _counters;
    private readonly IUserClock _clock;

    public UserStatsProvider(IMizanDbContext context, IActivityCounters counters, IUserClock clock)
    {
        _context = context;
        _counters = counters;
        _clock = clock;
    }

    public async Task<UserActivityStats> BuildAsync(
        Guid userId,
        IReadOnlySet<string> criteriaTypes,
        CancellationToken cancellationToken = default)
    {
        // One row for five criteria types, instead of five COUNT(*)s that get
        // slower every time the user logs anything.
        var counters = criteriaTypes.Overlaps(CriteriaTypes.Counted)
            ? await _counters.GetAsync(userId, cancellationToken)
            : new Domain.Entities.UserActivityCounters { UserId = userId };

        return new UserActivityStats
        {
            MealsLogged = counters.MealsLogged,
            RecipesCreated = counters.RecipesCreated,
            WorkoutsLogged = counters.WorkoutsLogged,
            BodyMeasurementsLogged = counters.BodyMeasurementsLogged,
            GoalProgressLogged = counters.GoalProgressLogged,

            StreakNutrition = await StreakAsync(userId, "nutrition", criteriaTypes, cancellationToken),
            StreakWorkout = await StreakAsync(userId, "workout", criteriaTypes, cancellationToken),

            TotalVolumeKg = criteriaTypes.Contains(CriteriaTypes.TotalVolumeKg)
                ? await _context.ExerciseSets
                    .Where(s => s.WorkoutExercise.Workout.UserId == userId && s.Completed)
                    .SumAsync(s => (s.WeightKg ?? 0) * (s.Reps ?? 0), cancellationToken)
                : 0,

            TemplateCompletedCount = criteriaTypes.Contains(CriteriaTypes.TemplateCompletedCount)
                ? await _context.Workouts.CountAsync(
                    w => w.UserId == userId && w.TemplateId != null, cancellationToken)
                : 0,

            FollowersCount = criteriaTypes.Contains(CriteriaTypes.FollowersCount)
                ? await _context.Follows.CountAsync(
                    f => f.FolloweeUserId == userId && f.Status == "Accepted", cancellationToken)
                : 0,

            WorkoutsShared = criteriaTypes.Contains(CriteriaTypes.WorkoutsShared)
                ? await _context.FeedItems.CountAsync(
                    f => f.UserId == userId && f.WorkoutId != null, cancellationToken)
                : 0,

            ReactionsGiven = criteriaTypes.Contains(CriteriaTypes.ReactionsGiven)
                ? await _context.FeedReactions.Where(r => r.UserId == userId)
                    .Select(r => r.FeedItemId).Distinct().CountAsync(cancellationToken)
                : 0,

            CommentsMade = criteriaTypes.Contains(CriteriaTypes.CommentsMade)
                ? await _context.FeedComments.Where(c => c.UserId == userId && c.DeletedAt == null)
                    .Select(c => c.FeedItemId).Distinct().CountAsync(cancellationToken)
                : 0,

            PrCount = criteriaTypes.Contains(CriteriaTypes.PrCount)
                ? PersonalRecords.Count(await BestWeightsAsync(userId, cancellationToken))
                : 0,

            EarnedPoints = criteriaTypes.Contains(CriteriaTypes.PointsTotal)
                ? await _context.UserAchievements.Where(ua => ua.UserId == userId)
                    .Join(_context.Achievements, ua => ua.AchievementId, a => a.Id, (_, a) => a.Points)
                    .SumAsync(cancellationToken)
                : 0,
        };
    }

    /// <summary>
    /// The live length, not the stored one. A lapsed streak is zero here, so a
    /// badge for thirty consecutive days cannot be unlocked by a streak that
    /// died three weeks ago.
    /// </summary>
    private async Task<int> StreakAsync(
        Guid userId, string type, IReadOnlySet<string> criteriaTypes, CancellationToken cancellationToken)
    {
        var criteria = type == "nutrition" ? CriteriaTypes.StreakNutrition : CriteriaTypes.StreakWorkout;
        if (!criteriaTypes.Contains(criteria)) return 0;

        var stored = await _context.Streaks.AsNoTracking()
            .Where(s => s.UserId == userId && s.StreakType == type)
            .Select(s => new { s.CurrentCount, s.LongestCount, s.LastActivityDate, s.FreezesAvailable })
            .FirstOrDefaultAsync(cancellationToken);

        if (stored is null) return 0;

        var state = await _clock.EvaluateAsync(
            userId,
            stored.CurrentCount,
            stored.LongestCount,
            stored.LastActivityDate,
            stored.FreezesAvailable,
            cancellationToken);

        return state.CurrentCount;
    }

    private async Task<List<WorkoutBestWeight>> BestWeightsAsync(Guid userId, CancellationToken cancellationToken) =>
        await _context.ExerciseSets
            .Where(set => set.WorkoutExercise.Workout.UserId == userId && set.Completed && set.WeightKg > 0)
            .GroupBy(set => new
            {
                set.WorkoutExercise.ExerciseId,
                set.WorkoutExercise.WorkoutId,
                set.WorkoutExercise.Workout.WorkoutDate,
                set.WorkoutExercise.Workout.CreatedAt,
            })
            .Select(group => new WorkoutBestWeight(
                group.Key.ExerciseId,
                group.Key.WorkoutId,
                group.Key.WorkoutDate,
                group.Key.CreatedAt,
                group.Max(set => set.WeightKg!.Value)))
            .ToListAsync(cancellationToken);
}
