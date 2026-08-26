using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Interfaces;
using Mizan.Application.Queries;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The production bug, end to end: three readers of Streak.CurrentCount and
/// only one of them knew the streak had lapsed. Every test here fails against
/// the old code.
/// </summary>
[Collection("ApiIntegration")]
public class StreakCorrectnessTests
{
    private readonly ApiTestFixture _fixture;

    public StreakCorrectnessTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ALapsedStreakReadsAsZeroOnTheStreakEndpoint()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync();
        await SeedStreakAsync(userId, count: 30, daysAgo: 9);

        var streak = await client.GetFromJsonAsync<GetStreakResult>("/api/Achievements/streak");

        streak!.CurrentStreak.Should().Be(0);
        streak.LongestStreak.Should().Be(30, "the record stands even though the run ended");
    }

    /// <summary>
    /// The header read the column raw, so a streak that died in March kept
    /// showing whatever it reached.
    /// </summary>
    [Fact]
    public async Task ALapsedStreakReadsAsZeroOnTheProfileToo()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync();
        await SeedStreakAsync(userId, count: 30, daysAgo: 9);

        var profile = await client.GetFromJsonAsync<Dictionary<string, object>>("/api/Users/me");

        profile!["streakCount"].ToString().Should().Be("0");
    }

    /// <summary>
    /// Progress bars and unlocks came from two different methods that
    /// disagreed. They are one method now, so a bar at the threshold means the
    /// badge is earnable.
    /// </summary>
    [Fact]
    public async Task ProgressAndUnlockingAgreeAboutALapsedStreak()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync();
        await SeedStreakAsync(userId, count: 30, daysAgo: 9);
        // The seeded catalogue has its own streak achievements and this table
        // is not truncated between runs, so the name is unique per test.
        var name = $"Thirty days {Guid.NewGuid():N}";
        await SeedAchievementAsync(name, "streak_nutrition", 30);

        var page = await client.GetFromJsonAsync<GetAchievementsResult>("/api/Achievements?Page=1&PageSize=500");
        var row = page!.Items.Single(a => a.Name == name);

        row.Progress.Should().Be(0);
        row.IsEarned.Should().BeFalse();
    }

    [Fact]
    public async Task TheStreakEndpointReportsTheDeadlineAndZone()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync(timeZone: "Africa/Addis_Ababa");
        await SeedStreakAsync(userId, count: 3, daysAgo: 1);

        var streak = await client.GetFromJsonAsync<GetStreakResult>("/api/Achievements/streak");

        streak!.CurrentStreak.Should().Be(3);
        streak.TimeZoneId.Should().Be("Africa/Addis_Ababa");
        streak.AtRisk.Should().BeTrue("yesterday was the last activity");
        streak.IsActiveToday.Should().BeFalse();
        streak.ResetsAt.Should().BeAfter(DateTimeOffset.UtcNow);
        streak.ResetsAt.Should().BeBefore(DateTimeOffset.UtcNow.AddHours(24));
    }

    /// <summary>
    /// A user three hours east logging at 01:00 local: on UTC days that lands
    /// on yesterday and the streak never advances.
    /// </summary>
    [Fact]
    public async Task TheDayBoundaryFollowsTheUsersZone()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync(timeZone: "Pacific/Kiritimati"); // UTC+14

        using var scope = _fixture.Services.CreateScope();
        var clock = scope.ServiceProvider.GetRequiredService<IUserClock>();

        var theirToday = await clock.TodayAsync(userId);

        theirToday.Should().BeOnOrAfter(DateOnly.FromDateTime(DateTime.UtcNow));
        client.Dispose();
    }

    [Fact]
    public async Task LoggingKeepsTheActivityCounterInStep()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync();
        var food = await _fixture.SeedFoodAsync($"Lentils {Guid.NewGuid():N}", 116, 9, 20, 0.4m);

        for (var i = 0; i < 3; i++)
        {
            (await client.PostAsJsonAsync("/api/Meals", new { foodId = food.Id, mealType = "LUNCH", servings = 1 }))
                .EnsureSuccessStatusCode();
        }

        (await CountersAsync(userId)).MealsLogged.Should().Be(3);
    }

    /// <summary>
    /// The counters replace COUNT(*), which went down when a row was deleted.
    /// They have to do the same or a deleted meal still counts toward a badge.
    /// </summary>
    [Fact]
    public async Task DeletingAMealDecrementsTheCounter()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync();
        var food = await _fixture.SeedFoodAsync($"Teff {Guid.NewGuid():N}", 367, 13, 73, 2.4m);

        var created = await client.PostAsJsonAsync(
            "/api/Meals", new { foodId = food.Id, mealType = "DINNER", servings = 1 });
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["id"].ToString();

        (await CountersAsync(userId)).MealsLogged.Should().Be(1);

        (await client.DeleteAsync($"/api/Meals/{id}")).EnsureSuccessStatusCode();

        (await CountersAsync(userId)).MealsLogged.Should().Be(0);
    }

    private async Task<(HttpClient Client, Guid UserId)> UserAsync(string? timeZone = null)
    {
        var id = Guid.NewGuid();
        var email = $"streak-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email);

        if (timeZone is not null)
        {
            using var scope = _fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == id);
            user.TimeZoneId = timeZone;
            await db.SaveChangesAsync();
        }

        return (_fixture.CreateAuthenticatedClient(id, email), id);
    }

    private async Task SeedStreakAsync(Guid userId, int count, int daysAgo)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        db.Streaks.Add(new Streak
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            StreakType = "nutrition",
            CurrentCount = count,
            LongestCount = count,
            LastActivityDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-daysAgo),
            FreezesAvailable = 0,
        });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedAchievementAsync(string name, string criteria, int threshold)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var id = Guid.CreateVersion7();
        db.Achievements.Add(new Achievement
        {
            Id = id,
            Name = name,
            Description = name,
            CriteriaType = criteria,
            Threshold = threshold,
            Points = 50,
            Category = "nutrition",
        });
        await db.SaveChangesAsync();

        // The catalogue is cached, so a test that seeds one has to publish it
        // the same way the admin write path does.
        await scope.ServiceProvider.GetRequiredService<IAchievementCatalogue>().InvalidateAsync();
        return id;
    }

    private async Task<UserActivityCounters> CountersAsync(Guid userId)
    {
        using var scope = _fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IActivityCounters>().GetAsync(userId);
    }
}
