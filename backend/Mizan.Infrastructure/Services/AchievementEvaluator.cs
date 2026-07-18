using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Services;

public class AchievementEvaluator : IAchievementEvaluator
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationWriter? _notifications;

    public AchievementEvaluator(IMizanDbContext context, ICurrentUserService currentUser, INotificationWriter? notifications = null)
    {
        _context = context;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task<IReadOnlyList<UnlockedAchievement>> EvaluateAsync(
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<string>? criteriaTypes = null)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Array.Empty<UnlockedAchievement>();
        }

        var userId = _currentUser.UserId.Value;

        var earnedIds = await _context.UserAchievements
            .Where(ua => ua.UserId == userId)
            .Select(ua => ua.AchievementId)
            .ToListAsync(cancellationToken);

        var candidateQuery = _context.Achievements
            .Where(a => a.CriteriaType != null && !earnedIds.Contains(a.Id));
        if (criteriaTypes is not null)
        {
            candidateQuery = candidateQuery.Where(a => a.CriteriaType == "points_total" || criteriaTypes.Contains(a.CriteriaType!));
        }
        var candidates = await candidateQuery.ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return Array.Empty<UnlockedAchievement>();
        }

        var stats = await BuildStatsAsync(userId, candidates.Select(candidate => candidate.CriteriaType!).ToHashSet(), cancellationToken);

        // Pass 1: evaluate everything except points-based criteria (which depend on this pass's results).
        var unlocks = new List<Achievement>();
        foreach (var achievement in candidates)
        {
            if (achievement.CriteriaType == "points_total") continue;
            if (MeetsCriteria(achievement, stats)) unlocks.Add(achievement);
        }

        if (unlocks.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var a in unlocks)
            {
                _context.UserAchievements.Add(new UserAchievement
                {
                    UserId = userId,
                    AchievementId = a.Id,
                    EarnedAt = now
                });
            }
            stats.EarnedPoints += unlocks.Sum(a => a.Points);
        }

        // Pass 2: evaluate points-based achievements with the updated total.
        foreach (var achievement in candidates)
        {
            if (achievement.CriteriaType != "points_total") continue;
            if (unlocks.Contains(achievement)) continue;
            if (MeetsCriteria(achievement, stats)) unlocks.Add(achievement);
        }

        if (unlocks.Count == 0)
        {
            return Array.Empty<UnlockedAchievement>();
        }

        // Persist any points-based unlocks added in pass 2.
        var alreadyStagedIds = _context.UserAchievements.Local
            .Where(ua => ua.UserId == userId)
            .Select(ua => ua.AchievementId)
            .ToHashSet();

        foreach (var a in unlocks)
        {
            if (alreadyStagedIds.Contains(a.Id)) continue;
            _context.UserAchievements.Add(new UserAchievement
            {
                UserId = userId,
                AchievementId = a.Id,
                EarnedAt = DateTime.UtcNow
            });
        }

        if (_notifications is not null)
        {
            foreach (var achievement in unlocks)
            {
                await _notifications.AddAsync(userId, "achievement_unlocked", $"Achievement unlocked: {achievement.Name}", achievement.Description, "/achievements", cancellationToken);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return unlocks
            .Select(a => new UnlockedAchievement(a.Id, a.Name, a.Description, a.IconUrl, a.Points, a.Category))
            .ToList();
    }

    private async Task<UserStats> BuildStatsAsync(Guid userId, IReadOnlySet<string> criteriaTypes, CancellationToken ct)
    {
        var mealsLogged = criteriaTypes.Contains("meals_logged")
            ? await _context.FoodDiaryEntries.CountAsync(e => e.UserId == userId, ct)
            : 0;
        var recipesCreated = criteriaTypes.Contains("recipes_created")
            ? await _context.Recipes.CountAsync(r => r.UserId == userId, ct)
            : 0;
        var workoutsLogged = criteriaTypes.Contains("workouts_logged")
            ? await _context.Workouts.CountAsync(w => w.UserId == userId, ct)
            : 0;
        var measurementsLogged = criteriaTypes.Contains("body_measurements_logged")
            ? await _context.BodyMeasurements.CountAsync(m => m.UserId == userId, ct)
            : 0;
        var goalProgressLogged = criteriaTypes.Contains("goal_progress_logged")
            ? await _context.GoalProgress.CountAsync(g => g.UserId == userId, ct)
            : 0;
        var totalVolumeKg = criteriaTypes.Contains("total_volume_kg")
            ? await _context.ExerciseSets.Where(s => s.WorkoutExercise.Workout.UserId == userId && s.Completed)
                .SumAsync(s => (s.WeightKg ?? 0) * (s.Reps ?? 0), ct)
            : 0;
        var templateCompleted = criteriaTypes.Contains("template_completed_count")
            ? await _context.Workouts.CountAsync(w => w.UserId == userId && w.TemplateId != null, ct)
            : 0;
        var followers = criteriaTypes.Contains("followers_count")
            ? await _context.Follows.CountAsync(f => f.FolloweeUserId == userId && f.Status == "Accepted", ct)
            : 0;
        var workoutsShared = criteriaTypes.Contains("workouts_shared")
            ? await _context.FeedItems.CountAsync(f => f.UserId == userId && f.WorkoutId != null, ct)
            : 0;
        var reactionsGiven = criteriaTypes.Contains("reactions_given")
            ? await _context.FeedReactions.Where(r => r.UserId == userId).Select(r => r.FeedItemId).Distinct().CountAsync(ct)
            : 0;
        var commentsMade = criteriaTypes.Contains("comments_made")
            ? await _context.FeedComments.Where(c => c.UserId == userId && c.DeletedAt == null).Select(c => c.FeedItemId).Distinct().CountAsync(ct)
            : 0;
        var workoutBestWeights = criteriaTypes.Contains("pr_count")
            ? await _context.ExerciseSets
            .Where(set => set.WorkoutExercise.Workout.UserId == userId && set.Completed && set.WeightKg > 0)
            .GroupBy(set => new
            {
                set.WorkoutExercise.ExerciseId,
                set.WorkoutExercise.WorkoutId,
                set.WorkoutExercise.Workout.WorkoutDate,
                set.WorkoutExercise.Workout.CreatedAt
            })
            .Select(group => new WorkoutBestWeight(
                group.Key.ExerciseId,
                group.Key.WorkoutId,
                group.Key.WorkoutDate,
                group.Key.CreatedAt,
                group.Max(set => set.WeightKg!.Value)))
            .ToListAsync(ct)
            : [];
        var prCount = PersonalRecords.Count(workoutBestWeights);

        var nutritionStreak = criteriaTypes.Contains("streak_nutrition") ? await _context.Streaks
            .Where(s => s.UserId == userId && s.StreakType == "nutrition")
            .Select(s => s.CurrentCount)
            .FirstOrDefaultAsync(ct) : 0;

        var workoutStreak = criteriaTypes.Contains("streak_workout") ? await _context.Streaks
            .Where(s => s.UserId == userId && s.StreakType == "workout")
            .Select(s => s.CurrentCount)
            .FirstOrDefaultAsync(ct) : 0;

        var earnedPoints = criteriaTypes.Contains("points_total") ? await _context.UserAchievements
            .Where(ua => ua.UserId == userId)
            .Join(_context.Achievements, ua => ua.AchievementId, a => a.Id, (ua, a) => a.Points)
            .SumAsync(ct) : 0;

        return new UserStats
        {
            MealsLogged = mealsLogged,
            RecipesCreated = recipesCreated,
            WorkoutsLogged = workoutsLogged,
            BodyMeasurementsLogged = measurementsLogged,
            GoalProgressLogged = goalProgressLogged,
            TotalVolumeKg = totalVolumeKg,
            TemplateCompletedCount = templateCompleted,
            FollowersCount = followers,
            WorkoutsShared = workoutsShared,
            ReactionsGiven = reactionsGiven,
            CommentsMade = commentsMade,
            PrCount = prCount,
            StreakNutrition = nutritionStreak,
            StreakWorkout = workoutStreak,
            EarnedPoints = earnedPoints
        };
    }

    private static bool MeetsCriteria(Achievement a, UserStats s) => a.CriteriaType switch
    {
        "meals_logged" => s.MealsLogged >= a.Threshold,
        "recipes_created" => s.RecipesCreated >= a.Threshold,
        "workouts_logged" => s.WorkoutsLogged >= a.Threshold,
        "body_measurements_logged" => s.BodyMeasurementsLogged >= a.Threshold,
        "goal_progress_logged" => s.GoalProgressLogged >= a.Threshold,
        "streak_nutrition" => s.StreakNutrition >= a.Threshold,
        "streak_workout" => s.StreakWorkout >= a.Threshold,
        "points_total" => s.EarnedPoints >= a.Threshold,
        "total_volume_kg" => s.TotalVolumeKg >= a.Threshold,
        "template_completed_count" => s.TemplateCompletedCount >= a.Threshold,
        "followers_count" => s.FollowersCount >= a.Threshold,
        "workouts_shared" => s.WorkoutsShared >= a.Threshold,
        "reactions_given" => s.ReactionsGiven >= a.Threshold,
        "comments_made" => s.CommentsMade >= a.Threshold,
        "pr_count" => s.PrCount >= a.Threshold,
        _ => false
    };

    private sealed class UserStats
    {
        public int MealsLogged { get; set; }
        public int RecipesCreated { get; set; }
        public int WorkoutsLogged { get; set; }
        public int BodyMeasurementsLogged { get; set; }
        public int GoalProgressLogged { get; set; }
        public decimal TotalVolumeKg { get; set; }
        public int TemplateCompletedCount { get; set; }
        public int FollowersCount { get; set; }
        public int WorkoutsShared { get; set; }
        public int ReactionsGiven { get; set; }
        public int CommentsMade { get; set; }
        public int PrCount { get; set; }
        public int StreakNutrition { get; set; }
        public int StreakWorkout { get; set; }
        public int EarnedPoints { get; set; }
    }
}
