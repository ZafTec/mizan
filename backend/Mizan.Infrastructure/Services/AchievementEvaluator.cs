using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Services;

public class AchievementEvaluator : IAchievementEvaluator
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AchievementEvaluator(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<UnlockedAchievement>> EvaluateAsync(CancellationToken cancellationToken = default)
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

        var candidates = await _context.Achievements
            .Where(a => a.CriteriaType != null && !earnedIds.Contains(a.Id))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return Array.Empty<UnlockedAchievement>();
        }

        var stats = await BuildStatsAsync(userId, cancellationToken);

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

        await _context.SaveChangesAsync(cancellationToken);

        return unlocks
            .Select(a => new UnlockedAchievement(a.Id, a.Name, a.Description, a.IconUrl, a.Points, a.Category))
            .ToList();
    }

    private async Task<UserStats> BuildStatsAsync(Guid userId, CancellationToken ct)
    {
        var mealsLogged = await _context.FoodDiaryEntries.CountAsync(e => e.UserId == userId, ct);
        var recipesCreated = await _context.Recipes.CountAsync(r => r.UserId == userId, ct);
        var workoutsLogged = await _context.Workouts.CountAsync(w => w.UserId == userId, ct);
        var measurementsLogged = await _context.BodyMeasurements.CountAsync(m => m.UserId == userId, ct);
        var goalProgressLogged = await _context.GoalProgress.CountAsync(g => g.UserId == userId, ct);

        var nutritionStreak = await _context.Streaks
            .Where(s => s.UserId == userId && s.StreakType == "nutrition")
            .Select(s => s.CurrentCount)
            .FirstOrDefaultAsync(ct);

        var workoutStreak = await _context.Streaks
            .Where(s => s.UserId == userId && s.StreakType == "workout")
            .Select(s => s.CurrentCount)
            .FirstOrDefaultAsync(ct);

        var earnedPoints = await _context.UserAchievements
            .Where(ua => ua.UserId == userId)
            .Join(_context.Achievements, ua => ua.AchievementId, a => a.Id, (ua, a) => a.Points)
            .SumAsync(ct);

        return new UserStats
        {
            MealsLogged = mealsLogged,
            RecipesCreated = recipesCreated,
            WorkoutsLogged = workoutsLogged,
            BodyMeasurementsLogged = measurementsLogged,
            GoalProgressLogged = goalProgressLogged,
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
        _ => false
    };

    private sealed class UserStats
    {
        public int MealsLogged { get; set; }
        public int RecipesCreated { get; set; }
        public int WorkoutsLogged { get; set; }
        public int BodyMeasurementsLogged { get; set; }
        public int GoalProgressLogged { get; set; }
        public int StreakNutrition { get; set; }
        public int StreakWorkout { get; set; }
        public int EarnedPoints { get; set; }
    }
}
