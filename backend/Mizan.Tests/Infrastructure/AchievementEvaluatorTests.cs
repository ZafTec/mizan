using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Mizan.Infrastructure.Services;
using Xunit;

namespace Mizan.Tests.Infrastructure;

public class AchievementEvaluatorTests
{
    private static (MizanDbContext db, AchievementEvaluator svc, Guid userId) Make()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new MizanDbContext(options);
        var userId = Guid.NewGuid();
        var user = new FakeCurrentUser { UserId = userId };
        return (db, new AchievementEvaluator(db, user), userId);
    }

    private static Achievement Ach(string name, string criteria, int threshold, int points, string category = "nutrition") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        CriteriaType = criteria,
        Threshold = threshold,
        Points = points,
        Category = category
    };

    [Fact]
    public async Task Evaluate_UnlocksMealsLogged_WhenThresholdMet()
    {
        var (db, svc, userId) = Make();
        var ach = Ach("Ten Meals", "meals_logged", 10, 25);
        db.Achievements.Add(ach);
        for (int i = 0; i < 10; i++)
            db.FoodDiaryEntries.Add(new FoodDiaryEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "x",
                MealType = "MEAL",
                Servings = 1,
                EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
                LoggedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var unlocks = await svc.EvaluateAsync();

        unlocks.Should().ContainSingle(u => u.Id == ach.Id);
        (await db.UserAchievements.CountAsync(ua => ua.UserId == userId && ua.AchievementId == ach.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Evaluate_DoesNotUnlock_WhenBelowThreshold()
    {
        var (db, svc, userId) = Make();
        db.Achievements.Add(Ach("Ten Meals", "meals_logged", 10, 25));
        for (int i = 0; i < 9; i++)
            db.FoodDiaryEntries.Add(new FoodDiaryEntry
            {
                Id = Guid.NewGuid(), UserId = userId, Name = "x", MealType = "MEAL",
                Servings = 1, EntryDate = DateOnly.FromDateTime(DateTime.UtcNow), LoggedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var unlocks = await svc.EvaluateAsync();

        unlocks.Should().BeEmpty();
        (await db.UserAchievements.CountAsync(ua => ua.UserId == userId)).Should().Be(0);
    }

    [Fact]
    public async Task Evaluate_DoesNotReUnlock_AlreadyEarned()
    {
        var (db, svc, userId) = Make();
        var ach = Ach("First Meal", "meals_logged", 1, 10);
        db.Achievements.Add(ach);
        db.UserAchievements.Add(new UserAchievement
        {
            UserId = userId, AchievementId = ach.Id, EarnedAt = DateTime.UtcNow.AddDays(-1)
        });
        db.FoodDiaryEntries.Add(new FoodDiaryEntry
        {
            Id = Guid.NewGuid(), UserId = userId, Name = "x", MealType = "MEAL",
            Servings = 1, EntryDate = DateOnly.FromDateTime(DateTime.UtcNow), LoggedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var unlocks = await svc.EvaluateAsync();

        unlocks.Should().BeEmpty();
        (await db.UserAchievements.CountAsync(ua => ua.UserId == userId && ua.AchievementId == ach.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Evaluate_PointsTotal_UsesFreshlyUnlockedPoints_InSamePass()
    {
        var (db, svc, userId) = Make();
        // Primary unlock worth 100 points, points_total threshold = 100.
        // Second pass must see the 100 just added and unlock the points badge.
        var primary = Ach("First Meal", "meals_logged", 1, 100);
        var pointsBadge = Ach("Bronze Badge", "points_total", 100, 0, category: "milestone");
        db.Achievements.AddRange(primary, pointsBadge);
        db.FoodDiaryEntries.Add(new FoodDiaryEntry
        {
            Id = Guid.NewGuid(), UserId = userId, Name = "x", MealType = "MEAL",
            Servings = 1, EntryDate = DateOnly.FromDateTime(DateTime.UtcNow), LoggedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var unlocks = await svc.EvaluateAsync();

        unlocks.Select(u => u.Id).Should().Contain(new[] { primary.Id, pointsBadge.Id });
        (await db.UserAchievements.CountAsync(ua => ua.UserId == userId)).Should().Be(2);
    }

    [Fact]
    public async Task Evaluate_IgnoresAchievements_WithoutCriteriaType()
    {
        var (db, svc, userId) = Make();
        db.Achievements.Add(new Achievement
        {
            Id = Guid.NewGuid(), Name = "Manual-only", CriteriaType = null, Threshold = 0, Points = 50
        });
        await db.SaveChangesAsync();

        var unlocks = await svc.EvaluateAsync();

        unlocks.Should().BeEmpty();
    }

    [Fact]
    public async Task Evaluate_Unauthenticated_ReturnsEmpty()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new MizanDbContext(options);
        var svc = new AchievementEvaluator(db, new FakeCurrentUser { UserId = null });

        var unlocks = await svc.EvaluateAsync();

        unlocks.Should().BeEmpty();
    }

    [Fact]
    public async Task Evaluate_StreakCriteria_UsesLiveStreakRowCount()
    {
        var (db, svc, userId) = Make();
        db.Streaks.Add(new Streak
        {
            Id = Guid.NewGuid(), UserId = userId, StreakType = "nutrition",
            CurrentCount = 7, LongestCount = 7, LastActivityDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        var ach = Ach("7-day nutrition", "streak_nutrition", 7, 50, category: "consistency");
        db.Achievements.Add(ach);
        await db.SaveChangesAsync();

        var unlocks = await svc.EvaluateAsync();

        unlocks.Should().ContainSingle(u => u.Id == ach.Id);
    }
}
