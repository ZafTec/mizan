extern alias telegram;
using FluentAssertions;
using Mizan.Contracts.Telegram;
using Xunit;
using static Mizan.Tests.Application.TelegramFakeWorld;

namespace Mizan.Tests.Application;

/// <summary>
/// Reminders at the bot's two wires: /remind walks through what, which days and what time and posts
/// the API's own request; /reminders lists them with Stop; a reminder card's buttons call the reminder.
/// </summary>
public class TelegramRemindTests
{
    private const string ReminderId = "77777777-7777-4777-8777-777777777777";

    private readonly TelegramFakeWorld _world = new();

    [Fact]
    public async Task Remind_AsksWhatWhichDaysAndWhatTime_ThenPostsTheReminder()
    {
        await _world.Handler().HandleAsync(Text("/remind"), CancellationToken.None);
        _world.LastKeyboard().Should().Equal(("Workout", "rm:k:workout"), ("Measurements", "rm:k:measurement"), ("Log a meal", "rm:k:meal_log"));

        await _world.Handler().HandleAsync(Press("rm:k:workout"), CancellationToken.None);
        _world.LastKeyboard().Should().Equal(
            ("Every day", "rm:d:workout:daily"), ("Weekdays", "rm:d:workout:wk"), ("Weekends", "rm:d:workout:we"), ("Every other day", "rm:d:workout:2d"));

        await _world.Handler().HandleAsync(Press("rm:d:workout:wk"), CancellationToken.None);
        _world.LastKeyboard().Select(b => b.Data).Should().Contain("rm:t:workout:wk:0700")
            .And.OnlyContain(d => System.Text.Encoding.UTF8.GetByteCount(d) <= 64);

        await _world.Handler().HandleAsync(Press("rm:t:workout:wk:0700"), CancellationToken.None);
        var (method, path, body) = _world.ReminderCalls.Should().ContainSingle().Subject;
        (method, path).Should().Be(("POST", "api/Reminders"));
        body.GetProperty("kind").GetString().Should().Be("workout");
        body.GetProperty("schedule").GetProperty("repeat").GetString().Should().Be("days");
        body.GetProperty("schedule").GetProperty("time").GetString().Should().Be("07:00");
        body.GetProperty("schedule").GetProperty("days").EnumerateArray().Select(d => d.GetString()).Should().Equal("mon", "tue", "wed", "thu", "fri");
        _world.LastText.Should().Be("Set: <b>Drink water</b>, Weekdays at 07:00. First one Mon 2 Nov at 07:00.\nSee them all with <code>/reminders</code>.");
    }

    [Fact]
    public async Task ButtonsAlone_SetTheReminder_AndTellTheAssistantConversationEachStep()
    {
        await _world.Handler().HandleAsync(Text("/remind"), CancellationToken.None);
        await _world.Handler().HandleAsync(Press("rm:k:meal_log"), CancellationToken.None);
        await _world.Handler().HandleAsync(Press("rm:d:meal_log:wk"), CancellationToken.None);
        await _world.Handler().HandleAsync(Press("rm:t:meal_log:wk:1200"), CancellationToken.None);

        _world.ReminderCalls.Should().ContainSingle(c => c.Method == "POST");
        _world.ChatPosted.Should().BeEmpty();
        var notes = _world.ContextPosted.Select(n => (Note: n.GetProperty("note").GetString()!, Hold: n.GetProperty("holdMinutes").GetInt32())).ToList();
        notes.Should().HaveCount(3);
        notes[0].Note.Should().Contain("Log a meal (kind meal_log)").And.Contain("\"Which days?\"");
        notes[1].Note.Should().Contain("days: Weekdays").And.Contain("\"What time?\"");
        notes.Take(2).Should().OnlyContain(n => n.Hold == 15);
        notes[2].Should().Match<(string Note, int Hold)>(n => n.Hold == 0 && n.Note.Contains("finished"));
        _world.PendingContext.Should().BeNull();
    }

    [Fact]
    public async Task WordsTypedWhileAStepWaits_GoToTheAssistantWithThatStep_AndOnlyOnce()
    {
        await _world.Handler().HandleAsync(Text("/remind"), CancellationToken.None);
        await _world.Handler().HandleAsync(Press("rm:k:meal_log"), CancellationToken.None);

        await _world.Handler().HandleAsync(Text("once a week"), CancellationToken.None);
        await _world.Handler().HandleAsync(Text("thanks"), CancellationToken.None);

        _world.ReminderCalls.Should().BeEmpty();
        _world.ChatPosted.Should().HaveCount(2);
        _world.ChatPosted[0].GetProperty("message").GetString().Should().Be("once a week");
        _world.ChatPosted[0].GetProperty("clientContext").GetString().Should()
            .Contain("Log a meal (kind meal_log)").And.Contain("\"Which days?\"");
        _world.ChatPosted[1].GetProperty("clientContext").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task RemindWithWords_CarriesTheWordsInTheMessage_AndPostsACustomReminder()
    {
        await _world.Handler().HandleAsync(Text("/remind drink water"), CancellationToken.None);
        _world.LastText.Should().StartWith("Remind you to: <b>drink water</b>");
        _world.ContextPosted.Should().ContainSingle().Which.GetProperty("note").GetString().Should().Contain("titled \"drink water\"");

        // Telegram hands back the message as plain text with the button that was pressed.
        await _world.Handler().HandleAsync(Press("rm:d:custom:daily", "Remind you to: drink water\nWhich days?"), CancellationToken.None);
        await _world.Handler().HandleAsync(Press("rm:t:custom:daily:1200", "Remind you to: drink water\nWhat time?"), CancellationToken.None);

        var body = _world.ReminderCalls.Should().ContainSingle().Subject.Body;
        body.GetProperty("kind").GetString().Should().Be("custom");
        body.GetProperty("title").GetString().Should().Be("drink water");
        body.GetProperty("schedule").GetProperty("repeat").GetString().Should().Be("daily");
    }

    [Fact]
    public async Task EveryOtherDay_PostsATwoDayInterval_AtTheChosenTime()
    {
        await _world.Handler().HandleAsync(Press("rm:t:measurement:2d:0800"), CancellationToken.None);

        var schedule = _world.ReminderCalls.Should().ContainSingle().Subject.Body.GetProperty("schedule");
        schedule.GetProperty("repeat").GetString().Should().Be("every");
        schedule.GetProperty("everyDays").GetInt32().Should().Be(2);
        schedule.GetProperty("time").GetString().Should().Be("08:00");
    }

    [Fact]
    public void AFailedAssistantAction_ShowsItsReasonOnce_WithoutAnEmptyLabel()
    {
        var failed = new Mizan.Contracts.Ai.AiToolInvocation("create_reminder", "", false, "Say how often it repeats.");

        telegram::Mizan.Telegram.Bot.UpdateHandler.PerformedLines([failed, failed])
            .Should().Be("\n\n✗ Say how often it repeats.");
    }

    [Theory]
    [InlineData(TelegramReminderCallbacks.Done, "/done")]
    [InlineData(TelegramReminderCallbacks.Snooze, "/snooze")]
    [InlineData(TelegramReminderCallbacks.Skip, "/skip")]
    public async Task AReminderCardsButton_CallsThatReminder_AndRepliesWithWhatTheApiSaid(string action, string suffix)
    {
        await _world.Handler().HandleAsync(Press(TelegramReminderCallbacks.Data(action, Guid.Parse(ReminderId))), CancellationToken.None);

        var (method, path, _) = _world.ReminderCalls.Should().ContainSingle().Subject;
        (method, path).Should().Be(("POST", $"api/Reminders/{ReminderId}{suffix}"));
        _world.LastText.Should().Be("Snoozed. I will remind you again at 08:47.");
    }

    [Fact]
    public async Task Reminders_ListsEachWithStop_AndStopTurnsItOff()
    {
        _world.RemindersJson = $$"""
            {"items":[{"id":"{{ReminderId}}","title":"Drink water","summary":"Every day at 12:00","nextRunText":"Mon 2 Nov at 12:00","enabled":true}],
             "activeCount":1,"activeLimit":3,"isPro":false}
            """;
        await _world.Handler().HandleAsync(Text("/reminders"), CancellationToken.None);
        _world.LastText.Should().Be("<b>Your reminders</b> (1 of 3 on)\n\nDrink water: Every day at 12:00, next Mon 2 Nov at 12:00");
        _world.LastKeyboard().Should().Equal(("Stop: Drink water", $"ra:stop:{Guid.Parse(ReminderId):N}"));

        await _world.Handler().HandleAsync(Press($"ra:stop:{Guid.Parse(ReminderId):N}"), CancellationToken.None);
        var (method, path, body) = _world.ReminderCalls.Last();
        (method, path).Should().Be(("PUT", $"api/Reminders/{ReminderId}"));
        body.GetProperty("enabled").GetBoolean().Should().BeFalse();
        _world.LastText.Should().StartWith("Stopped <b>Drink water</b>.");
    }
}
