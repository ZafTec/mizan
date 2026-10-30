using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mizan.Contracts.Notifications;
using Mizan.Contracts.Planning;
using Mizan.Domain.Entities;
using Mizan.Domain.Notifications;
using Mizan.Infrastructure.Data;
using Mizan.Infrastructure.Outbox;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The evening reminder of a plan with an eating window: the scan queues it, the outbox sends it
/// as a notification, a push and, for a person who opted in, a Telegram message, once. Times are set on the test clock, on
/// today's date in the person's zone, because the diary only takes meals for about today.
/// </summary>
[Collection("ApiIntegration")]
public class EveningReminderTests
{
    private const string Text = "Your window closes at 20:00. Today: 1,640 of 1,800 kcal.";

    private static readonly TimeSpan AddisOffset = TimeSpan.FromHours(3);

    private readonly ApiTestFixture _fixture;

    public EveningReminderTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task APersonWhoseWindowClosesAt20_IsRemindedAt1930InTheirZone_AndOnlyOnce()
    {
        await _fixture.ResetDatabaseAsync();
        _fixture.Push.Reset();
        var world = new PlanningWorld(_fixture);
        var today = Mizan.Domain.Streaks.StreakClock.Today("Africa/Addis_Ababa", DateTimeOffset.UtcNow);
        DateTimeOffset At(int hour, int minute) => new(today.ToDateTime(new TimeOnly(hour, minute)), AddisOffset);
        var (userId, client) = await world.PersonAsync("evening");
        await _fixture.GrantProAsync(userId);
        await world.DbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            user.TimeZoneId = "Africa/Addis_Ababa";
            user.NotifyByTelegram = true;
            db.TelegramLinks.Add(new TelegramLink { Id = Guid.NewGuid(), UserId = userId, TelegramUserId = 4242, LinkedAt = DateTime.UtcNow });
            return await db.SaveChangesAsync();
        });
        (await client.PostAsJsonAsync("/api/Devices", new { Token = "evening-phone", Platform = "android" })).EnsureSuccessStatusCode();

        var planId = await PlanningWorld.CreatePlanAsync(client, new CreateMealPlanRequest
        {
            Name = "Window week",
            StartDate = today.AddDays(-1),
            DurationDays = 7,
            Rules = new PlanRulesDto
            {
                TargetCalories = 1800,
                EatingWindowStart = new TimeOnly(8, 0),
                EatingWindowEnd = new TimeOnly(20, 0),
            },
        });
        var meal = await client.PostAsJsonAsync("/api/Meals", new
        {
            name = "Lunch",
            mealType = "LUNCH",
            servings = 1,
            calories = 1640,
            entryDate = today.ToString("yyyy-MM-dd"),
        });
        meal.StatusCode.Should().Be(HttpStatusCode.Created, await meal.Content.ReadAsStringAsync());

        try
        {
            // 19:20 and 19:30 in Addis Ababa: two scans find the same reminder, and it is queued once.
            _fixture.Clock.Set(At(19, 20));
            await ScanAsync();
            _fixture.Clock.Set(At(19, 30));
            await ScanAsync();
            (await ReminderJobsAsync()).Should().Be(1);
            await RunDueJobsAsync();

            // Later the same evening, with the window still open: nothing more to send today.
            _fixture.Clock.Set(At(19, 45));
            await ScanAsync();
            await RunDueJobsAsync();
        }
        finally
        {
            _fixture.Clock.Reset();
        }

        var notifications = await client.GetFromJsonAsync<JsonElement>("/api/Notifications");
        var reminder = notifications.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        reminder.GetProperty("type").GetString().Should().Be("evening_reminder");
        reminder.GetProperty("body").GetString().Should().Be(Text);
        reminder.GetProperty("linkUrl").GetString().Should().Be($"/meal-plan/{planId}?day={today:yyyy-MM-dd}");

        _fixture.Push.Sent.Should().ContainSingle().Which.Body.Should().Be(Text);
        var telegram = _fixture.TelegramBot.Sent.Should().ContainSingle().Subject;
        telegram.TelegramUserId.Should().Be(4242);
        telegram.Text.Should().Be($"Eating window\n{Text}");
        telegram.OpenPath.Should().Be($"plan?planId={planId:D}&day={today:yyyy-MM-dd}");
        (await ReminderJobsAsync()).Should().Be(1);
    }

    [Fact]
    public async Task EveryMemberFollowingAHouseholdPlan_IsRemindedInTheirOwnZone_UnlessTheyFollowTheirOwnPlan()
    {
        await _fixture.ResetDatabaseAsync();
        _fixture.Push.Reset();
        var world = new PlanningWorld(_fixture);
        var (ownerId, ownerClient) = await world.PersonAsync("household-owner");
        var (tokyoId, tokyoClient) = await world.PersonAsync("household-tokyo");
        var (ownPlanId, ownPlanClient) = await world.PersonAsync("household-own-plan");
        await _fixture.GrantProAsync(tokyoId);
        var household = await world.HouseholdAsync(ownerId, (tokyoId, true), (ownPlanId, true));
        await world.DbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == ownerId)).TimeZoneId = "Africa/Addis_Ababa";
            var tokyoUser = await db.Users.SingleAsync(u => u.Id == tokyoId);
            tokyoUser.TimeZoneId = "Asia/Tokyo";
            tokyoUser.NotifyByTelegram = true;
            (await db.Users.SingleAsync(u => u.Id == ownPlanId)).TimeZoneId = "Africa/Addis_Ababa";
            db.TelegramLinks.Add(new TelegramLink { Id = Guid.NewGuid(), UserId = tokyoId, TelegramUserId = 777, LinkedAt = DateTime.UtcNow });
            return await db.SaveChangesAsync();
        });

        var addisToday = Mizan.Domain.Streaks.StreakClock.Today("Africa/Addis_Ababa", DateTimeOffset.UtcNow);
        var tokyoToday = Mizan.Domain.Streaks.StreakClock.Today("Asia/Tokyo", DateTimeOffset.UtcNow);
        var householdPlan = await PlanningWorld.CreatePlanAsync(ownerClient, new CreateMealPlanRequest
        {
            Name = "Family week",
            StartDate = addisToday.AddDays(-1),
            DurationDays = 7,
            HouseholdId = household,
            Rules = new PlanRulesDto { TargetCalories = 1800, EatingWindowStart = new TimeOnly(8, 0), EatingWindowEnd = new TimeOnly(20, 0) },
        });
        var ownPlan = await PlanningWorld.CreatePlanAsync(ownPlanClient, new CreateMealPlanRequest
        {
            Name = "My week",
            StartDate = addisToday.AddDays(-1),
            DurationDays = 7,
            Rules = new PlanRulesDto { TargetCalories = 2000, EatingWindowStart = new TimeOnly(8, 0), EatingWindowEnd = new TimeOnly(23, 0) },
        });
        var meal = await tokyoClient.PostAsJsonAsync("/api/Meals", new
        {
            name = "Lunch",
            mealType = "LUNCH",
            servings = 1,
            calories = 1200,
            entryDate = tokyoToday.ToString("yyyy-MM-dd"),
        });
        meal.StatusCode.Should().Be(HttpStatusCode.Created, await meal.Content.ReadAsStringAsync());

        // Each reminder's time, half an hour before its window closes in its person's zone. They
        // lie hours apart, so each scan queues only the one due then; run them in order.
        var times = new[]
        {
            new DateTimeOffset(tokyoToday.ToDateTime(new TimeOnly(19, 30)), TimeSpan.FromHours(9)),
            new DateTimeOffset(addisToday.ToDateTime(new TimeOnly(19, 30)), AddisOffset),
            new DateTimeOffset(addisToday.ToDateTime(new TimeOnly(22, 30)), AddisOffset),
        }.Order().ToList();
        try
        {
            foreach (var at in times)
            {
                _fixture.Clock.Set(at);
                await ScanAsync();
                await RunDueJobsAsync();
                _fixture.Clock.Set(at.AddMinutes(10));
                await ScanAsync();
                await RunDueJobsAsync();
            }
        }
        finally
        {
            _fixture.Clock.Reset();
        }

        async Task<JsonElement> ReminderOf(HttpClient client) =>
            (await client.GetFromJsonAsync<JsonElement>("/api/Notifications")).GetProperty("items").EnumerateArray()
                .Should().ContainSingle(n => n.GetProperty("type").GetString() == "evening_reminder").Subject;

        var owner = await ReminderOf(ownerClient);
        owner.GetProperty("body").GetString().Should().Be("Your window closes at 20:00. Today: 0 of 1,800 kcal.");
        owner.GetProperty("linkUrl").GetString().Should().Be($"/meal-plan/{householdPlan}?day={addisToday:yyyy-MM-dd}");

        var tokyo = await ReminderOf(tokyoClient);
        tokyo.GetProperty("body").GetString().Should().Be("Your window closes at 20:00. Today: 1,200 of 1,800 kcal.");
        tokyo.GetProperty("linkUrl").GetString().Should().Be($"/meal-plan/{householdPlan}?day={tokyoToday:yyyy-MM-dd}");

        var own = await ReminderOf(ownPlanClient);
        own.GetProperty("body").GetString().Should().Be("Your window closes at 23:00. Today: 0 of 2,000 kcal.");
        own.GetProperty("linkUrl").GetString().Should().Be($"/meal-plan/{ownPlan}?day={addisToday:yyyy-MM-dd}");

        var telegram = _fixture.TelegramBot.Sent.Should().ContainSingle().Subject;
        telegram.TelegramUserId.Should().Be(777);
        telegram.OpenPath.Should().Be($"plan?planId={householdPlan:D}&day={tokyoToday:yyyy-MM-dd}");
        (await ReminderJobsAsync()).Should().Be(3);
    }

    [Fact]
    public async Task APersonWhoTurnedTheAppOffForReminders_IsStillRemindedOnlyOnceADay_WhenTheWindowMoves()
    {
        await _fixture.ResetDatabaseAsync();
        _fixture.Push.Reset();
        var world = new PlanningWorld(_fixture);
        var today = Mizan.Domain.Streaks.StreakClock.Today("Africa/Addis_Ababa", DateTimeOffset.UtcNow);
        DateTimeOffset At(int hour, int minute) => new(today.ToDateTime(new TimeOnly(hour, minute)), AddisOffset);
        var (userId, client) = await world.PersonAsync("telegram-only");
        await _fixture.GrantProAsync(userId);
        await world.DbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            user.TimeZoneId = "Africa/Addis_Ababa";
            user.NotifyByTelegram = true;
            db.TelegramLinks.Add(new TelegramLink { Id = Guid.NewGuid(), UserId = userId, TelegramUserId = 4343, LinkedAt = DateTime.UtcNow });
            return await db.SaveChangesAsync();
        });
        var off = await client.PutAsJsonAsync("/api/Notifications/preferences", new UpdateNotificationPreferencesRequest(
            [new NotificationKindPreferenceUpdate(NotificationKinds.Reminders, InApp: false)]));
        off.StatusCode.Should().Be(HttpStatusCode.OK, await off.Content.ReadAsStringAsync());

        var rules = new PlanRulesDto { TargetCalories = 1800, EatingWindowStart = new TimeOnly(8, 0), EatingWindowEnd = new TimeOnly(20, 0) };
        var planId = await PlanningWorld.CreatePlanAsync(client, new CreateMealPlanRequest
        {
            Name = "Window week",
            StartDate = today.AddDays(-1),
            DurationDays = 7,
            Rules = rules,
        });

        try
        {
            _fixture.Clock.Set(At(19, 30));
            await ScanAsync();
            await RunDueJobsAsync();

            // The window moves to 21:00 after the reminder went out. The next scan finds today's
            // reminder on Telegram although the app holds no row for it, and queues no second one.
            var moved = await client.PutAsJsonAsync($"/api/MealPlans/{planId}", new UpdateMealPlanRequest
            {
                Name = "Window week",
                StartDate = today.AddDays(-1),
                DurationDays = 7,
                Rules = rules with { EatingWindowEnd = new TimeOnly(21, 0) },
            });
            moved.IsSuccessStatusCode.Should().BeTrue(await moved.Content.ReadAsStringAsync());
            _fixture.Clock.Set(At(20, 30));
            await ScanAsync();
            await RunDueJobsAsync();
        }
        finally
        {
            _fixture.Clock.Reset();
        }

        _fixture.TelegramBot.Sent.Where(m => m.TelegramUserId == 4343).Should().ContainSingle()
            .Which.Text.Should().StartWith("Eating window\nYour window closes at 20:00.");
        var notifications = await client.GetFromJsonAsync<JsonElement>("/api/Notifications");
        notifications.GetProperty("items").EnumerateArray().Should().BeEmpty();
        (await ReminderJobsAsync()).Should().Be(1);
    }

    private Task ScanAsync() =>
        _fixture.Services.GetServices<IHostedService>().OfType<NotificationSchedulerService>().Single().RunOnceAsync(CancellationToken.None);

    /// <summary>
    /// The test clock has reached the reminders' time, but the database's has not, and the
    /// dispatcher claims by the database's. Bring their time forward, then run the queue.
    /// </summary>
    private async Task RunDueJobsAsync()
    {
        using (var scope = _fixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<MizanDbContext>().OutboxJobs
                .Where(j => j.Type == OutboxJobTypes.EveningReminder && j.Status == OutboxJobStatus.Pending)
                .ExecuteUpdateAsync(j => j.SetProperty(x => x.RunAfter, DateTime.UtcNow.AddSeconds(-1)));
        }
        await _fixture.DrainOutboxAsync();
    }

    private async Task<int> ReminderJobsAsync()
    {
        using var scope = _fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<MizanDbContext>().OutboxJobs.CountAsync(j => j.Type == OutboxJobTypes.EveningReminder);
    }
}
