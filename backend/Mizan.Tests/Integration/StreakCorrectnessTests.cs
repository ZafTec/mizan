using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        await SeedStreakAsync(userId, count: 3, daysAgo: 1, relativeToUsersZone: true);

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

    /// <summary>
    /// A meal logged three days late used to be saved and then answered 500,
    /// because the streak refused a day that far from today, so a retry logged
    /// it twice. A date years away, usually a typo, failed the same way.
    /// </summary>
    [Fact]
    public async Task AMealLoggedDaysLateIsSaved_AndADateYearsAwayIsRefusedWithAReason()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync(timeZone: "Pacific/Kiritimati"); // UTC+14
        await SeedStreakAsync(userId, count: 4, daysAgo: 0, relativeToUsersZone: true);
        var food = await _fixture.SeedFoodAsync($"Lentils {Guid.NewGuid():N}", 116, 9, 20, 0.4m);
        var lateDay = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3);

        var late = await client.PostAsJsonAsync("/api/Meals", new
        {
            name = "Shiro",
            entryDate = lateDay.ToString("yyyy-MM-dd"),
            mealType = "DINNER",
            servings = 1,
            calories = 420,
        });

        late.StatusCode.Should().Be(HttpStatusCode.Created, await late.Content.ReadAsStringAsync());
        (await late.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("streak").ValueKind
            .Should().Be(JsonValueKind.Null, "a day that far back fills the diary and leaves the streak alone");

        foreach (var (date, reason) in new[] { ("1900-01-01", "10 years back"), ("2500-01-01", "a year ahead") })
        {
            var far = await client.PostAsJsonAsync("/api/Meals", new
            {
                name = "Shiro",
                entryDate = date,
                mealType = "DINNER",
                servings = 1,
                calories = 420,
            });

            far.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var problem = await far.Content.ReadFromJsonAsync<JsonElement>();
            problem.GetProperty("errorCode").GetString().Should().Be("validation_failed");
            problem.GetProperty("error").GetString().Should().Contain(reason).And.Contain(date);
        }

        // The food log takes the same range, and without a date it is today in the user's zone, the streak's day.
        var farFood = await client.PostAsJsonAsync("/api/Nutrition/log", new { foodId = food.Id, entryDate = "2500-01-01", mealType = "LUNCH", servings = 1 });
        farFood.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await farFood.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().Should().Contain("2500-01-01");
        (await client.PostAsJsonAsync("/api/Nutrition/log", new { foodId = food.Id, mealType = "LUNCH", servings = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _fixture.Services.CreateScope();
        var theirToday = await scope.ServiceProvider.GetRequiredService<IUserClock>().TodayAsync(userId);
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        (await db.FoodDiaryEntries.Where(e => e.UserId == userId).Select(e => e.EntryDate).ToListAsync())
            .Should().BeEquivalentTo([lateDay, theirToday]);
        (await db.Streaks.SingleAsync(s => s.UserId == userId)).CurrentCount.Should().Be(4);
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

    private async Task SeedStreakAsync(Guid userId, int count, int daysAgo, bool relativeToUsersZone = false)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

        // Near a UTC day boundary, "yesterday" in UTC and "yesterday" in the
        // user's zone can be different dates. Match whichever one the
        // assertion actually depends on instead of always using UTC.
        var today = relativeToUsersZone
            ? await scope.ServiceProvider.GetRequiredService<IUserClock>().TodayAsync(userId)
            : DateOnly.FromDateTime(DateTime.UtcNow);

        db.Streaks.Add(new Streak
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            StreakType = "nutrition",
            CurrentCount = count,
            LongestCount = count,
            LastActivityDate = today.AddDays(-daysAgo),
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
