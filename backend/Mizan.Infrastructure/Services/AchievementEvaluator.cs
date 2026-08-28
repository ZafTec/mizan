using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Achievements;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Services;

public class AchievementEvaluator : IAchievementEvaluator
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserStatsProvider _stats;
    private readonly IAchievementCatalogue _catalogue;
    private readonly INotificationWriter? _notifications;

    public AchievementEvaluator(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        IUserStatsProvider stats,
        IAchievementCatalogue catalogue,
        INotificationWriter? notifications = null)
    {
        _context = context;
        _currentUser = currentUser;
        _stats = stats;
        _catalogue = catalogue;
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

        // The catalogue is seed data that changes when an admin edits it, not
        // when a user logs a meal. Reading it from Redis keeps a table scan off
        // the hot path.
        var earned = earnedIds.ToHashSet();
        var candidates = (await _catalogue.MeasurableAsync(cancellationToken))
            .Where(a => !earned.Contains(a.Id))
            .Where(a => criteriaTypes is null
                || a.CriteriaType == CriteriaTypes.PointsTotal
                || criteriaTypes.Contains(a.CriteriaType!))
            .ToList();

        if (candidates.Count == 0)
        {
            return Array.Empty<UnlockedAchievement>();
        }

        var stats = await _stats.BuildAsync(
            userId, candidates.Select(candidate => candidate.CriteriaType!).ToHashSet(), cancellationToken);

        // Pass 1: evaluate everything except points-based criteria (which depend on this pass's results).
        var unlocks = new List<Achievement>();
        foreach (var achievement in candidates)
        {
            if (achievement.CriteriaType == CriteriaTypes.PointsTotal) continue;
            if (stats.Meets(achievement.CriteriaType, achievement.Threshold)) unlocks.Add(achievement);
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
            stats = stats with { EarnedPoints = stats.EarnedPoints + unlocks.Sum(a => a.Points) };
        }

        // Pass 2: evaluate points-based achievements with the updated total.
        foreach (var achievement in candidates)
        {
            if (achievement.CriteriaType != CriteriaTypes.PointsTotal) continue;
            if (unlocks.Contains(achievement)) continue;
            if (stats.Meets(achievement.CriteriaType, achievement.Threshold)) unlocks.Add(achievement);
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

}
