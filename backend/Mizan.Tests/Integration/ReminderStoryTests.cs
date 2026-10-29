using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mizan.Application.Notifications;
using Mizan.Contracts.Ai;
using Mizan.Contracts.Notifications;
using Mizan.Contracts.Telegram;
using Mizan.Contracts.Users;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Mizan.Infrastructure.Outbox;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// Reminders (docs/ARCHITECTURE.md#reminders): a person's own schedules, set from any surface through
/// the same commands, fired by the one notification scheduler on the person's clock.
/// </summary>
[Collection("ApiIntegration")]
public class ReminderStoryTests
{
    private static readonly ReminderSchedule WeekdaysAtSeven = new("days", "07:00", ["mon", "tue", "wed", "thu", "fri"]);

    private readonly ApiTestFixture _fixture;

    public ReminderStoryTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AWeekdayWorkoutReminderAtSevenInAddisAbaba_IsQueuedAtFourUtcOnMonday()
    {
        await _fixture.ResetDatabaseAsync();
        var world = new PlanningWorld(_fixture);
        var (userId, client) = await world.PersonAsync("addis");
        await world.DbAsync(db => db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(u => u.SetProperty(x => x.TimeZoneId, "Africa/Addis_Ababa")));

        // Sunday 1 November 2026, 12:00 in Addis Ababa.
        _fixture.Clock.Set(new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero));
        try
        {
            var reminder = await CreateAsync(client, new CreateReminderRequest("workout", Schedule: WeekdaysAtSeven, Channels: ["inApp"]));
            reminder.Summary.Should().Be("Weekdays at 07:00");
            reminder.NextRunAt.Should().Be(new DateTime(2026, 11, 2, 4, 0, 0, DateTimeKind.Utc));
            reminder.NextRunText.Should().Be("Mon 2 Nov at 07:00");
            reminder.Title.Should().Be("Time for your workout");

            // Two hours before, the scan queues Monday's one delivery.
            _fixture.Clock.Set(new DateTimeOffset(2026, 11, 2, 2, 30, 0, TimeSpan.Zero));
            await ScanAsync();
            var job = (await DeliveryJobsAsync()).Should().ContainSingle().Subject;
            job.RunAfter.Should().Be(new DateTime(2026, 11, 2, 4, 0, 0, DateTimeKind.Utc));
            job.DedupeKey.Should().Be($"schedule:{reminder.Id:N}:20261102T0400:{userId:N}:inApp");

            _fixture.Clock.Set(new DateTimeOffset(2026, 11, 2, 4, 0, 0, TimeSpan.Zero));
            await RunDueAsync();
        }
        finally
        {
            _fixture.Clock.Reset();
        }

        var inbox = await client.GetFromJsonAsync<NotificationListResult>("/api/Notifications");
        var row = inbox!.Items.Should().ContainSingle().Subject;
        row.Title.Should().Be("Time for your workout");
        row.Kind.Should().Be(Mizan.Domain.Notifications.NotificationKinds.Reminders);
        row.Actions.Should().ContainSingle().Which.Path.Should().Be("/workouts");

        // More often than hourly is refused, whichever surface asks.
        var tooOften = await client.PostAsJsonAsync("/api/Reminders", new CreateReminderRequest("custom", "Drink water", Schedule: new ReminderSchedule("every", EveryMinutes: 30)));
        tooOften.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await tooOften.Content.ReadAsStringAsync()).Should().Contain("A reminder can repeat at most once an hour.");
    }

    [Fact]
    public async Task AMonthlyReminder_OnTheFirstSundayOrTheLastDay_FiresOnThatDayOfTheMonth()
    {
        await _fixture.ResetDatabaseAsync();
        var world = new PlanningWorld(_fixture);
        var (userId, client) = await world.PersonAsync("monthly");
        await world.DbAsync(db => db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(u => u.SetProperty(x => x.TimeZoneId, "Africa/Addis_Ababa")));

        // Monday 2 November 2026, 12:00 in Addis Ababa: November's first Sunday has passed.
        _fixture.Clock.Set(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));
        try
        {
            var firstSunday = await CreateAsync(client, new CreateReminderRequest(
                "measurement", Schedule: new ReminderSchedule("monthly", "08:00", ["sun"], Week: "first"), Channels: ["inApp"]));
            firstSunday.Summary.Should().Be("First Sunday of the month at 08:00");
            firstSunday.NextRunAt.Should().Be(new DateTime(2026, 12, 6, 5, 0, 0, DateTimeKind.Utc));
            firstSunday.NextRunText.Should().Be("Sun 6 Dec at 08:00");
            (firstSunday.Repeat, firstSunday.Week, firstSunday.DayOfMonth).Should().Be(("monthly", "first", (int?)null));

            var lastDay = await CreateAsync(client, new CreateReminderRequest(
                "custom", "Pay rent", Schedule: new ReminderSchedule("monthly", "08:00", DayOfMonth: 31), Channels: ["inApp"]));
            lastDay.Summary.Should().Be("The last day of the month at 08:00");
            lastDay.NextRunAt.Should().Be(new DateTime(2026, 11, 30, 5, 0, 0, DateTimeKind.Utc));
        }
        finally
        {
            _fixture.Clock.Reset();
        }

        var twoDays = await client.PostAsJsonAsync("/api/Reminders", new CreateReminderRequest(
            "custom", "Water plants", Schedule: new ReminderSchedule("monthly", "08:00", ["sat", "sun"], Week: "first")));
        twoDays.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await twoDays.Content.ReadAsStringAsync()).Should().Contain("Choose one weekday");
    }

    [Fact]
    public async Task PressingSnoozeInTelegram_QueuesTheReminderAgainThirtyMinutesLater()
    {
        await _fixture.ResetDatabaseAsync();
        var world = new PlanningWorld(_fixture);
        var (userId, client) = await world.PersonAsync("snoozer");
        await _fixture.GrantProAsync(userId);
        await world.DbAsync(db =>
        {
            db.TelegramLinks.Add(new TelegramLink { Id = Guid.NewGuid(), UserId = userId, TelegramUserId = 3001, LinkedAt = DateTime.UtcNow });
            return db.SaveChangesAsync();
        });
        (await client.PutAsJsonAsync("/api/Users/me", new UpdateUserRequest(null, null, NotifyByTelegram: true))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        _fixture.Clock.Set(new DateTimeOffset(2026, 11, 2, 5, 0, 0, TimeSpan.Zero));
        try
        {
            var reminder = await CreateAsync(client, new CreateReminderRequest("measurement", Schedule: new ReminderSchedule("daily", "08:15"), Channels: ["telegram"]));
            _fixture.Clock.Set(new DateTimeOffset(2026, 11, 2, 8, 15, 0, TimeSpan.Zero));
            await ScanAsync();
            await RunDueAsync();

            // The card the bot was asked to send carries the three buttons, each naming this reminder.
            var card = _fixture.TelegramBot.Sent.Should().ContainSingle().Subject.Card!;
            card.Title.Should().Be("Time to take your measurements");
            card.Callbacks.Should().Equal(TelegramReminderCallbacks.For(reminder.Id));
            var snooze = card.Callbacks!.Single(c => c.Label == "Snooze 30 min");
            TelegramReminderCallbacks.TryParse(snooze.Data, out var action, out var id).Should().BeTrue();
            (action, id).Should().Be((TelegramReminderCallbacks.Snooze, reminder.Id));

            // The bot presses it as the person, the way it calls every endpoint.
            using var bot = _fixture.CreateClient();
            bot.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
            bot.DefaultRequestHeaders.Add("X-Impersonate-User", userId.ToString());
            bot.DefaultRequestHeaders.Add(TelegramSurface.Header, TelegramSurface.Value);
            _fixture.Clock.Set(new DateTimeOffset(2026, 11, 2, 8, 17, 20, TimeSpan.Zero));
            var pressed = await bot.PostAsJsonAsync($"/api/Reminders/{reminder.Id}/snooze", new SnoozeReminderRequest());
            pressed.StatusCode.Should().Be(HttpStatusCode.OK, await pressed.Content.ReadAsStringAsync());
            var result = (await pressed.Content.ReadFromJsonAsync<ReminderActionResult>())!;
            result.Message.Should().Be("Snoozed. I will remind you again at 08:47.");
            result.SnoozedUntil.Should().Be(new DateTime(2026, 11, 2, 8, 47, 0, DateTimeKind.Utc));

            var again = (await DeliveryJobsAsync()).Where(j => j.Status == OutboxJobStatus.Pending).Should().ContainSingle().Subject;
            again.RunAfter.Should().Be(new DateTime(2026, 11, 2, 8, 47, 0, DateTimeKind.Utc));
            again.DedupeKey.Should().Be($"schedule:{reminder.Id:N}:20261102T0847:{userId:N}:telegram");

            // A second press in the same minute adds nothing.
            (await bot.PostAsJsonAsync($"/api/Reminders/{reminder.Id}/snooze", new SnoozeReminderRequest())).EnsureSuccessStatusCode();
            (await DeliveryJobsAsync()).Count(j => j.Status == OutboxJobStatus.Pending).Should().Be(1);

            _fixture.Clock.Set(new DateTimeOffset(2026, 11, 2, 8, 47, 0, TimeSpan.Zero));
            await RunDueAsync();
            _fixture.TelegramBot.Sent.Should().HaveCount(2);

            // Skip today then holds the rest of the person's day back.
            (await bot.PostAsync($"/api/Reminders/{reminder.Id}/skip", null)).EnsureSuccessStatusCode();
            var list = await client.GetFromJsonAsync<ReminderListResult>("/api/Reminders");
            list!.Items.Single().SkipUntil.Should().Be(new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc));
        }
        finally
        {
            _fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task TheAssistantsCreateReminderTool_MakesTheSameReminderAsTheApi_AndIsRefusedWithoutWriteConsent()
    {
        await _fixture.ResetDatabaseAsync();
        _fixture.Ai.Reset();
        var world = new PlanningWorld(_fixture);
        var (userId, client) = await world.PersonAsync("asker");
        await _fixture.GrantProAsync(userId);
        const string weekdays = """{"kind":"workout","repeat":"days","time":"07:00","days":["mon","tue","wed","thu","fri"]}""";

        await _fixture.GrantAiConsentAsync(userId, read: true, write: false);
        _fixture.Ai.CallTools(("create_reminder", weekdays));
        _fixture.Ai.Reply("Setting reminders is switched off.");
        var refused = await ChatAsync(client, "Remind me to work out on weekdays at 7");
        refused.Performed.Should().ContainSingle().Which.Succeeded.Should().BeFalse();
        refused.ConsentRequired.Should().Equal(new AiConsentRequirement(AiErrorCodes.ConsentRequired, "nutrition", "write"));
        (await ReminderRowsAsync(userId)).Should().BeEmpty();

        // The person turns writes on, as the consent page does.
        (await client.PutAsJsonAsync("/api/Ai/consent", new
        {
            enabled = true,
            shareNutrition = true,
            shareTraining = true,
            shareBody = true,
            allowWrites = true,
            writeNutrition = true,
            writeTraining = true,
            writeBody = true,
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        _fixture.Ai.CallTools(("create_reminder", weekdays));
        _fixture.Ai.Reply("Done.");
        var made = await ChatAsync(client, "Remind me to work out on weekdays at 7");
        made.Performed.Should().ContainSingle().Which.Succeeded.Should().BeTrue();

        var fromApi = await CreateAsync(client, new CreateReminderRequest("workout", Schedule: WeekdaysAtSeven));
        var rows = await ReminderRowsAsync(userId);
        rows.Should().HaveCount(2);
        var byAssistant = rows.Single(r => r.Id != fromApi.Id);
        var byApi = rows.Single(r => r.Id == fromApi.Id);
        byAssistant.Should().BeEquivalentTo(byApi, options => options
            .Excluding(r => r.Id)
            .Excluding(r => r.CreatedAt)
            .Excluding(r => r.UpdatedAt));
    }

    private static async Task<ReminderDto> CreateAsync(HttpClient client, CreateReminderRequest request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/Reminders") { Content = JsonContent.Create(request) };
        message.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await client.SendAsync(message);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ReminderDto>())!;
    }

    private static async Task<AiChatTurnDto> ChatAsync(HttpClient client, string message)
    {
        var response = await client.PostAsJsonAsync("/api/Ai/chat", new { threadId = (Guid?)null, message });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AiChatTurnDto>())!;
    }

    private async Task<List<NotificationSchedule>> ReminderRowsAsync(Guid userId)
    {
        using var scope = _fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<MizanDbContext>().NotificationSchedules.AsNoTracking()
            .Where(s => s.OwnerUserId == userId && s.Reminder != null)
            .ToListAsync();
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
    private async Task RunDueAsync()
    {
        using (var scope = _fixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<MizanDbContext>().OutboxJobs
                .Where(j => j.Type == OutboxJobTypes.NotificationDelivery && j.Status == OutboxJobStatus.Pending)
                .ExecuteUpdateAsync(j => j.SetProperty(x => x.RunAfter, DateTime.UtcNow.AddSeconds(-1)));
        }
        await _fixture.DrainOutboxAsync();
    }
}
