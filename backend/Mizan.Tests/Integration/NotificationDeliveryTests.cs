using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mizan.Application.Notifications;
using Mizan.Contracts.Notifications;
using Mizan.Contracts.Planning;
using Mizan.Contracts.Users;
using Mizan.Domain.Entities;
using Mizan.Domain.Notifications;
using Mizan.Infrastructure.Data;
using Mizan.Infrastructure.Outbox;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// One notification model and one scheduler (docs/ARCHITECTURE.md#notifications): a structured
/// message reaches Telegram as a card, a schedule fires at a local time in the recipient's zone
/// exactly once, and the person's choices per kind decide which channels it uses.
/// </summary>
[Collection("ApiIntegration")]
public class NotificationDeliveryTests
{
    private readonly ApiTestFixture _fixture;

    public NotificationDeliveryTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task EarningAnAchievementWhileLinked_QueuesATelegramCardCarryingTheBadge()
    {
        await _fixture.ResetDatabaseAsync();
        var world = new PlanningWorld(_fixture);
        var (userId, client) = await LinkedPersonAsync(world, "earner", 2001);
        await world.DbAsync(db =>
        {
            db.Achievements.Add(new Achievement
            {
                Id = Guid.NewGuid(),
                Name = "Card badge",
                Description = "Log a meal",
                Category = "nutrition",
                CriteriaType = "meals_logged",
                Threshold = 1,
                Points = 15,
                IconUrl = "/badges/128/achievement-first-meal.png",
            });
            return db.SaveChangesAsync();
        });

        (await LogMealAsync(client)).Should().Be(HttpStatusCode.Created);
        await _fixture.DrainOutboxAsync();

        var sent = _fixture.TelegramBot.Sent.Where(s => s.Card?.Title == "Achievement unlocked: Card badge").Should().ContainSingle().Subject;
        sent.TelegramUserId.Should().Be(2001);
        sent.Text.Should().Be("Achievement unlocked: Card badge\nLog a meal");
        sent.Card!.ImageUrl.Should().Be("http://localhost:3000/badges/512/achievement-first-meal.png");
        sent.Card.Facts.Should().ContainEquivalentOf(new NotificationFact("Points", "+15"));
        sent.Card.Buttons.Should().ContainSingle().Which.Should().Be(new Mizan.Contracts.Telegram.TelegramNotifyButton("See achievements", null, "/achievements"));

        // The app's own list carries the same picture, facts and action.
        var list = await client.GetFromJsonAsync<NotificationListResult>("/api/Notifications");
        var row = list!.Items.Should().ContainSingle(n => n.Title == "Achievement unlocked: Card badge").Subject;
        row.Kind.Should().Be(NotificationKinds.Achievements);
        row.ImageUrl.Should().Be(sent.Card.ImageUrl);
        row.Actions.Should().ContainSingle().Which.Path.Should().Be("/achievements");
        _ = userId;
    }

    [Fact]
    public async Task ADailySchedule_IsQueuedAtTheLocalTimeInTheRecipientsZone_AndNeverTwiceOnRetry()
    {
        await _fixture.ResetDatabaseAsync();
        var world = new PlanningWorld(_fixture);
        var (userId, client) = await LinkedPersonAsync(world, "tokyo", 2002);
        await world.DbAsync(db => db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(u => u.SetProperty(x => x.TimeZoneId, "Asia/Tokyo")));

        // 06:30 in Tokyo; the schedule fires at 08:00 there, 23:00 UTC the evening before.
        _fixture.Clock.Set(new DateTimeOffset(2026, 11, 2, 21, 30, 0, TimeSpan.Zero));
        try
        {
            Guid scheduleId;
            using (var scope = _fixture.Services.CreateScope())
            {
                var schedule = await scope.ServiceProvider.GetRequiredService<NotificationSchedules>().CreateForPersonAsync(
                    userId,
                    NotificationScheduleOwner.User,
                    "daily_check_in",
                    new NotificationMessage(NotificationKinds.Reminders, "Morning check-in", "Log breakfast when you can."),
                    "/today",
                    NotificationTrigger.Daily(new TimeOnly(8, 0)),
                    NotificationChannels.InApp | NotificationChannels.Telegram);
                await scope.ServiceProvider.GetRequiredService<MizanDbContext>().SaveChangesAsync();
                scheduleId = schedule.Id;
            }

            await ScanAsync();
            await ScanAsync();
            var jobs = await DeliveryJobsAsync();
            jobs.Should().HaveCount(2);
            jobs.Should().OnlyContain(j => j.RunAfter == new DateTime(2026, 11, 2, 23, 0, 0, DateTimeKind.Utc));
            jobs.Select(j => j.DedupeKey).Should().BeEquivalentTo(
                $"schedule:{scheduleId:N}:20261102T2300:{userId:N}:inApp",
                $"schedule:{scheduleId:N}:20261102T2300:{userId:N}:telegram");
            (await world.DbAsync(db => db.NotificationSchedules.AsNoTracking().SingleAsync(s => s.Id == scheduleId))).NextRunAt
                .Should().Be(new DateTime(2026, 11, 3, 23, 0, 0, DateTimeKind.Utc));

            // A scan that failed after staging is retried with the schedule as it was: nothing is added.
            await world.DbAsync(db => db.NotificationSchedules.Where(s => s.Id == scheduleId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextRunAt, new DateTime(2026, 11, 2, 23, 0, 0, DateTimeKind.Utc))));
            await ScanAsync();
            (await DeliveryJobsAsync()).Should().HaveCount(2);

            _fixture.Clock.Set(new DateTimeOffset(2026, 11, 2, 23, 0, 0, TimeSpan.Zero));
            await RunDueAsync(OutboxJobTypes.NotificationDelivery);
        }
        finally
        {
            _fixture.Clock.Reset();
        }

        var list = await client.GetFromJsonAsync<NotificationListResult>("/api/Notifications");
        list!.Items.Should().ContainSingle().Which.Title.Should().Be("Morning check-in");
        _fixture.TelegramBot.Sent.Should().ContainSingle().Which.Card!.Title.Should().Be("Morning check-in");
    }

    [Fact]
    public async Task TurningTelegramOffForAKind_StopsThatKindInTelegram_AndLeavesTheOthers()
    {
        await _fixture.ResetDatabaseAsync();
        var world = new PlanningWorld(_fixture);
        var (coachId, coach) = await world.PersonAsync("coach", role: "trainer");
        var (clientId, client) = await LinkedPersonAsync(world, "client", 2003);
        await world.CoachingAsync(coachId, clientId, canPlanMeals: true);
        await world.DbAsync(db =>
        {
            db.Achievements.Add(new Achievement { Id = Guid.NewGuid(), Name = "Still here", CriteriaType = "meals_logged", Threshold = 1, Points = 5 });
            return db.SaveChangesAsync();
        });

        var off = await client.PutAsJsonAsync("/api/Notifications/preferences", new UpdateNotificationPreferencesRequest(
            [new NotificationKindPreferenceUpdate(NotificationKinds.Planning, Telegram: false)]));
        off.StatusCode.Should().Be(HttpStatusCode.OK, await off.Content.ReadAsStringAsync());
        var planning = (await off.Content.ReadFromJsonAsync<NotificationPreferencesDto>())!.Kinds.Single(k => k.Kind == NotificationKinds.Planning);
        planning.Telegram.Should().BeFalse();
        planning.InApp.Should().BeTrue();

        var drafted = await coach.PostAsJsonAsync($"/api/Trainers/clients/{clientId}/meal-plans", new CreateMealPlanRequest
        {
            Name = "Week",
            StartDate = new DateOnly(2026, 11, 2),
            DurationDays = 7,
        });
        drafted.StatusCode.Should().Be(HttpStatusCode.Created, await drafted.Content.ReadAsStringAsync());
        (await LogMealAsync(client)).Should().Be(HttpStatusCode.Created);
        await _fixture.DrainOutboxAsync();

        // The plan draft is in the app only; the achievement still reaches Telegram.
        var list = await client.GetFromJsonAsync<NotificationListResult>("/api/Notifications");
        list!.Items.Should().Contain(n => n.Type == "meal_plan_drafted");
        _fixture.TelegramBot.Sent.Should().NotContain(s => s.Text.Contains("drafted a meal plan"));
        _fixture.TelegramBot.Sent.Should().Contain(s => s.Card!.Title == "Achievement unlocked: Still here");
    }

    [Fact]
    public async Task MarketingIsOffUntilThePersonTurnsItOn()
    {
        await _fixture.ResetDatabaseAsync();
        var world = new PlanningWorld(_fixture);
        var (userId, client) = await LinkedPersonAsync(world, "reader", 2004);

        var preferences = await client.GetFromJsonAsync<NotificationPreferencesDto>("/api/Notifications/preferences");
        var marketing = preferences!.Kinds.Single(k => k.Kind == NotificationKinds.Marketing);
        new[] { marketing.InApp, marketing.Telegram, marketing.Push, marketing.Email }.Should().AllBeEquivalentTo(false);
        preferences.Kinds.Where(k => k.Kind != NotificationKinds.Marketing).Should().OnlyContain(k => k.InApp && k.Telegram && k.Push && !k.Email);

        // A marketing message sent to everyone's channels reaches none of them.
        using (var scope = _fixture.Services.CreateScope())
        {
            var writer = scope.ServiceProvider.GetRequiredService<Mizan.Application.Interfaces.INotificationWriter>();
            (await writer.SendAsync(new Mizan.Application.Interfaces.NotificationSend(
                userId, "marketing", new NotificationMessage(NotificationKinds.Marketing, "New recipes this week")))).Should().BeNull();
            await scope.ServiceProvider.GetRequiredService<MizanDbContext>().SaveChangesAsync();
        }
        await _fixture.DrainOutboxAsync();

        (await client.GetFromJsonAsync<NotificationListResult>("/api/Notifications"))!.Items.Should().BeEmpty();
        _fixture.TelegramBot.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task AnEmailRaisedDuringQuietHours_WaitsUntilTheyEnd_WhileTheAppShowsItAtOnce()
    {
        await _fixture.ResetDatabaseAsync();
        var world = new PlanningWorld(_fixture);
        var (userId, client) = await LinkedPersonAsync(world, "sleeper", 2005);
        var set = await client.PutAsJsonAsync("/api/Notifications/preferences", new UpdateNotificationPreferencesRequest(
            [new NotificationKindPreferenceUpdate(NotificationKinds.Achievements, Email: true)], "22:00", "07:00"));
        set.StatusCode.Should().Be(HttpStatusCode.OK, await set.Content.ReadAsStringAsync());

        // 23:00 UTC, the person's clock: inside 22:00 to 07:00.
        _fixture.Clock.Set(new DateTimeOffset(2026, 11, 2, 23, 0, 0, TimeSpan.Zero));
        try
        {
            using (var scope = _fixture.Services.CreateScope())
            {
                var writer = scope.ServiceProvider.GetRequiredService<Mizan.Application.Interfaces.INotificationWriter>();
                await writer.SendAsync(new Mizan.Application.Interfaces.NotificationSend(
                    userId, "achievement_unlocked", new NotificationMessage(NotificationKinds.Achievements, "Achievement unlocked: Night owl")));
                await scope.ServiceProvider.GetRequiredService<MizanDbContext>().SaveChangesAsync();
            }

            var release = new DateTime(2026, 11, 3, 7, 0, 0, DateTimeKind.Utc);
            var (email, telegram) = await world.DbAsync(async db => (
                await db.OutboxJobs.AsNoTracking().SingleAsync(j => j.Type == OutboxJobTypes.Email),
                await db.OutboxJobs.AsNoTracking().SingleAsync(j => j.Type == OutboxJobTypes.TelegramMessage)));
            email.RunAfter.Should().Be(release, "email waits for quiet hours like Telegram");
            telegram.RunAfter.Should().Be(release);
            (await client.GetFromJsonAsync<NotificationListResult>("/api/Notifications"))!.Items
                .Should().ContainSingle(n => n.Title == "Achievement unlocked: Night owl");

            await _fixture.DrainOutboxAsync();
            (await world.DbAsync(db => db.EmailMessages.AsNoTracking().SingleAsync(m => m.ToUserId == userId))).Status
                .Should().Be(EmailMessageStatus.Queued, "nothing is sent before the quiet hours end");

            _fixture.Clock.Set(new DateTimeOffset(release));
            await RunDueAsync(OutboxJobTypes.Email);
            (await world.DbAsync(db => db.EmailMessages.AsNoTracking().SingleAsync(m => m.ToUserId == userId))).Status
                .Should().Be(EmailMessageStatus.Sent);
        }
        finally
        {
            _fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task AMeasurementThatReachesTheTargetWeight_SendsOneGoalCard_AndReachingItAgainSendsNothing()
    {
        await _fixture.ResetDatabaseAsync();
        var world = new PlanningWorld(_fixture);
        var (_, client) = await LinkedPersonAsync(world, "goal", 2010);
        (await client.PostAsJsonAsync("/api/Goals", new { goalType = "weight_loss", targetWeight = 72m, weightUnit = "kg" })).EnsureSuccessStatusCode();
        var today = DateTime.UtcNow.Date;

        // Heading down from 75 kg: 72.5 is not there yet, 71.8 is.
        await LogWeightAsync(client, today.AddDays(-3), 75m);
        await LogWeightAsync(client, today.AddDays(-2), 72.5m);
        await _fixture.DrainOutboxAsync();
        _fixture.TelegramBot.Sent.Should().NotContain(s => s.Card != null && s.Card.Title == "You reached your target weight");
        await LogWeightAsync(client, today.AddDays(-1), 71.8m);

        // Back above the target and down again: the goal's target was already reported.
        await LogWeightAsync(client, today, 72.4m);
        await LogWeightAsync(client, today, 71.6m);
        await _fixture.DrainOutboxAsync();

        var sent = _fixture.TelegramBot.Sent.Where(s => s.Card?.Title == "You reached your target weight").Should().ContainSingle().Subject;
        sent.TelegramUserId.Should().Be(2010);
        sent.Card!.Facts.Should().Equal(new NotificationFact("Target", "72 kg"), new NotificationFact("Logged", "71.8 kg"));
        sent.Card.Buttons.Should().ContainSingle().Which.Should().Be(new Mizan.Contracts.Telegram.TelegramNotifyButton("Open goal", null, "/goal"));

        var list = await client.GetFromJsonAsync<NotificationListResult>("/api/Notifications");
        var row = list!.Items.Should().ContainSingle(n => n.Type == "goal_reached").Subject;
        row.Kind.Should().Be(NotificationKinds.Achievements);
        row.Body.Should().Be("You logged 71.8 kg, which meets your target weight of 72 kg.");
        row.Actions.Should().ContainSingle().Which.Path.Should().Be("/goal");
    }

    [Fact]
    public async Task APersonWhoTurnedAchievementsOffInTelegram_SeesAReachedGoalOnlyInTheApp_AndOnce()
    {
        await _fixture.ResetDatabaseAsync();
        var world = new PlanningWorld(_fixture);
        var (_, client) = await LinkedPersonAsync(world, "goal-quiet", 2011);
        var off = await client.PutAsJsonAsync("/api/Notifications/preferences", new UpdateNotificationPreferencesRequest(
            [new NotificationKindPreferenceUpdate(NotificationKinds.Achievements, Telegram: false)]));
        off.StatusCode.Should().Be(HttpStatusCode.OK, await off.Content.ReadAsStringAsync());
        (await client.PostAsJsonAsync("/api/Goals", new { goalType = "muscle_gain", targetWeight = 176m, weightUnit = "lb" })).EnsureSuccessStatusCode();
        var today = DateTime.UtcNow.Date;

        // 176 lb is 79.83 kg. Heading up from 78 kg.
        await LogWeightAsync(client, today.AddDays(-2), 78m);
        await LogWeightAsync(client, today.AddDays(-1), 80m);
        await LogWeightAsync(client, today, 79m);
        await LogWeightAsync(client, today, 80.2m);
        await _fixture.DrainOutboxAsync();

        _fixture.TelegramBot.Sent.Should().NotContain(s => s.TelegramUserId == 2011);
        var list = await client.GetFromJsonAsync<NotificationListResult>("/api/Notifications");
        var row = list!.Items.Should().ContainSingle(n => n.Type == "goal_reached").Subject;
        row.Facts.Should().Equal(new NotificationFact("Target", "176 lb"), new NotificationFact("Logged", "176.4 lb"));
    }

    private static async Task LogWeightAsync(HttpClient client, DateTime date, decimal weightKg)
    {
        var logged = await client.PostAsJsonAsync("/api/BodyMeasurements", new { date, weightKg });
        logged.StatusCode.Should().Be(HttpStatusCode.OK, await logged.Content.ReadAsStringAsync());
    }

    /// <summary>A Pro person with a linked chat who turned Telegram notifications on.</summary>
    private async Task<(Guid Id, HttpClient Client)> LinkedPersonAsync(PlanningWorld world, string label, long telegramUserId)
    {
        var (id, client) = await world.PersonAsync(label);
        await _fixture.GrantProAsync(id);
        await world.DbAsync(db =>
        {
            db.TelegramLinks.Add(new TelegramLink { Id = Guid.NewGuid(), UserId = id, TelegramUserId = telegramUserId, LinkedAt = DateTime.UtcNow });
            return db.SaveChangesAsync();
        });
        var on = await client.PutAsJsonAsync("/api/Users/me", new UpdateUserRequest(null, null, NotifyByTelegram: true));
        on.StatusCode.Should().Be(HttpStatusCode.NoContent, await on.Content.ReadAsStringAsync());
        return (id, client);
    }

    private static async Task<HttpStatusCode> LogMealAsync(HttpClient client)
    {
        var meal = await client.PostAsJsonAsync("/api/Meals", new
        {
            name = "Lunch",
            mealType = "LUNCH",
            servings = 1,
            calories = 600,
            entryDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        });
        return meal.StatusCode;
    }

    private Task<int> ScanAsync() =>
        _fixture.Services.GetServices<IHostedService>().OfType<NotificationSchedulerService>().Single().RunOnceAsync(CancellationToken.None);

    private async Task<List<OutboxJob>> DeliveryJobsAsync()
    {
        using var scope = _fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<MizanDbContext>().OutboxJobs.AsNoTracking()
            .Where(j => j.Type == OutboxJobTypes.NotificationDelivery)
            .ToListAsync();
    }

    /// <summary>The dispatcher claims by the database's clock, which the test clock does not move. Bring the jobs forward, then run the queue.</summary>
    private async Task RunDueAsync(string type)
    {
        using (var scope = _fixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<MizanDbContext>().OutboxJobs
                .Where(j => j.Type == type && j.Status == OutboxJobStatus.Pending)
                .ExecuteUpdateAsync(j => j.SetProperty(x => x.RunAfter, DateTime.UtcNow.AddSeconds(-1)));
        }
        await _fixture.DrainOutboxAsync();
    }
}
