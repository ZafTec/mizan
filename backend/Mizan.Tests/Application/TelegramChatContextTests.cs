extern alias telegram;
using System.Net;
using FluentAssertions;
using Mizan.Contracts.Telegram;
using telegram::Mizan.Telegram.Bot;
using Xunit;
using static Mizan.Tests.Application.TelegramFakeWorld;

namespace Mizan.Tests.Application;

/// <summary>
/// What the bot tells the assistant conversation after a command or a button (docs/TELEGRAM.md#app-context):
/// the fact of what was pressed and what the API answered, held only while a button asks a question,
/// and never in the way of the button itself.
/// </summary>
public class TelegramChatContextTests
{
    private static readonly Guid ReminderId = Guid.Parse("77777777-7777-4777-8777-777777777777");

    private readonly TelegramFakeWorld _world = new();

    private List<(string Note, int Hold)> Notes() =>
        _world.ContextPosted.Select(n => (n.GetProperty("note").GetString()!, n.GetProperty("holdMinutes").GetInt32())).ToList();

    [Fact]
    public async Task A_reminders_Done_Snooze_Skip_and_Stop_buttons_tell_the_assistant_what_was_pressed_and_what_the_app_answered()
    {
        foreach (var action in new[] { TelegramReminderCallbacks.Done, TelegramReminderCallbacks.Snooze, TelegramReminderCallbacks.Skip, TelegramReminderCallbacks.Stop })
            await _world.Handler().HandleAsync(Press(TelegramReminderCallbacks.Data(action, ReminderId)), CancellationToken.None);

        var notes = Notes();
        notes.Should().HaveCount(4).And.OnlyContain(n => n.Hold == 0);
        notes[0].Note.Should().Be("The person pressed Done on the reminder \"Drink water\" in Telegram. The app answered: Snoozed. I will remind you again at 08:47.");
        notes[1].Note.Should().StartWith("The person pressed Snooze on the reminder \"Drink water\"");
        notes[2].Note.Should().StartWith("The person pressed Skip today on the reminder \"Drink water\"");
        notes[3].Note.Should().Be("The person pressed Stop on the reminder \"Drink water\" in Telegram: it is now off.");
        _world.PendingContext.Should().BeNull();
    }

    [Fact]
    public async Task Notification_switches_and_the_news_opt_out_are_reported_once_each_with_nothing_left_waiting()
    {
        _world.NotifyByTelegram = true;

        await _world.Handler().HandleAsync(Press("nt:off"), CancellationToken.None);
        await _world.Handler().HandleAsync(Press("nk:planning:off"), CancellationToken.None);
        await _world.Handler().HandleAsync(Press(TelegramMarketingCallbacks.OptOut), CancellationToken.None);

        Notes().Should().Equal(
            ("In Telegram the person turned notifications in this chat off.", 0),
            ("In Telegram the person turned Telegram notifications for Planning off.", 0),
            ("The person pressed the opt-out button on a Telegram message: news and offers are now off on every channel.", 0));
    }

    [Fact]
    public async Task Today_plan_shop_and_weight_report_what_the_chat_showed_in_plain_words()
    {
        await _world.Handler().HandleAsync(Text("/today"), CancellationToken.None);
        await _world.Handler().HandleAsync(Text("/plan"), CancellationToken.None);
        await _world.Handler().HandleAsync(Text("/shop"), CancellationToken.None);
        await _world.Handler().HandleAsync(Text("/weight 82.4"), CancellationToken.None);

        var notes = Notes();
        notes.Should().HaveCount(4).And.OnlyContain(n => n.Hold == 0 && !n.Note.Contains('<'));
        notes[0].Note.Should().StartWith("The /today command showed the person: Today").And.Contain("1860");
        notes[1].Note.Should().StartWith($"The /plan command showed the person: {_world.PlanName}");
        notes[2].Note.Should().StartWith("The /shop command showed the person:").And.Contain("left of 12");
        notes[3].Note.Should().Be("The /weight command logged a body weight of 82.4 kg for today.");
        _world.MeasurementsPosted.Should().ContainSingle();
    }

    [Fact]
    public async Task When_the_assistant_is_off_every_command_and_button_still_does_its_work()
    {
        _world.ContextStatus = HttpStatusCode.Forbidden;

        await _world.Handler().HandleAsync(Press("nt:on"), CancellationToken.None);
        _world.LastText.Should().StartWith("Notifications are on.");

        await _world.Handler().HandleAsync(Text("/today"), CancellationToken.None);
        _world.LastText.Should().StartWith("<b>Today</b>");

        _world.ContextPosted.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("<b>Today</b>\n1,860 kcal", "Today\n1,860 kcal")]
    [InlineData("Shiro &amp; injera &lt;3", "Shiro & injera <3")]
    [InlineData("  <i>(off)</i> ", "(off)")]
    public void A_card_is_reported_as_the_words_the_person_read(string card, string words) =>
        ChatContext.Plain(card).Should().Be(words);
}
