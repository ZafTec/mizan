using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mizan.Contracts.Planning;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Mizan.Infrastructure.Outbox;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The evening reminder of a plan with an eating window: the scan queues it, the outbox sends it
/// as a notification, a push and a Telegram message, once. Times are set on the test clock, on
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
            (await db.Users.SingleAsync(u => u.Id == userId)).TimeZoneId = "Africa/Addis_Ababa";
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
        reminder.GetProperty("linkUrl").GetString().Should().Be($"/meal-plan/{planId}");

        _fixture.Push.Sent.Should().ContainSingle().Which.Body.Should().Be(Text);
        var telegram = _fixture.TelegramBot.Sent.Should().ContainSingle().Subject;
        telegram.TelegramUserId.Should().Be(4242);
        telegram.Text.Should().Be(Text);
        telegram.OpenPath.Should().Be($"plan?planId={planId:D}&day={today:yyyy-MM-dd}");
        (await ReminderJobsAsync()).Should().Be(1);
    }

    private Task ScanAsync() =>
        _fixture.Services.GetServices<IHostedService>().OfType<EveningReminderScheduler>().Single().RunOnceAsync(CancellationToken.None);

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
