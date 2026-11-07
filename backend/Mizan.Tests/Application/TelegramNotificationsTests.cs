extern alias telegram;
using FluentAssertions;
using Xunit;
using static Mizan.Tests.Application.TelegramFakeWorld;

namespace Mizan.Tests.Application;

/// <summary>
/// <c>/notifications</c>: the chat reads and flips the same switch as the settings page, through
/// <c>/api/Users/me</c> as the linked user.
/// </summary>
public class TelegramNotificationsTests
{
    private readonly TelegramFakeWorld _world = new();

    [Fact]
    public async Task Notifications_SaysWhetherTheyComeHere_AndWhichKinds_WithAButtonForEach()
    {
        await _world.Handler().HandleAsync(Text("/notifications"), CancellationToken.None);

        _world.LastText.Should().StartWith("Notifications are <b>off</b> in this chat: they stay in the app.");
        _world.LastText.Should().Contain("Achievements: <b>on</b>").And.Contain("Marketing: <b>off</b>");
        _world.LastKeyboard().Should().Equal(
            ("Turn on in this chat", "nt:on"),
            ("Achievements: turn off", "nk:achievements:off"),
            ("Reminders: turn off", "nk:reminders:off"),
            ("Planning: turn off", "nk:planning:off"),
            ("Marketing: turn on", "nk:marketing:on"));
        _world.ProfilePuts.Should().BeEmpty();

        _world.NotifyByTelegram = true;
        await _world.Handler().HandleAsync(Text("/notifications"), CancellationToken.None);

        _world.LastText.Should().StartWith("Notifications are <b>on</b>");
        _world.LastKeyboard().First().Should().Be(("Turn off in this chat", "nt:off"));
    }

    [Fact]
    public async Task AKindsButton_AndTheKindByName_TurnTelegramOffAndOnForThatKindOnly()
    {
        _world.NotifyByTelegram = true;

        await _world.Handler().HandleAsync(Press("nk:planning:off"), CancellationToken.None);

        _world.PreferencePuts.Should().ContainSingle().Which.GetProperty("kinds")[0].GetProperty("telegram").GetBoolean().Should().BeFalse();
        _world.TelegramKinds["planning"].Should().BeFalse();
        _world.TelegramKinds["achievements"].Should().BeTrue();
        _world.LastText.Should().StartWith("Planning no longer come here.").And.Contain("Planning: <b>off</b>");
        _world.LastKeyboard().Should().Contain(("Planning: turn on", "nk:planning:on"));
        _world.ProfilePuts.Should().BeEmpty();

        await _world.Handler().HandleAsync(Text("/notifications planning on"), CancellationToken.None);

        _world.TelegramKinds["planning"].Should().BeTrue();
        _world.LastText.Should().StartWith("Planning now come here.");
    }

    [Fact]
    public async Task NotificationsOn_AndTheButton_SetTheSwitchThroughTheProfileEndpoint()
    {
        await _world.Handler().HandleAsync(Text("/notifications on"), CancellationToken.None);

        _world.ProfilePuts.Should().ContainSingle().Which.GetProperty("notifyByTelegram").GetBoolean().Should().BeTrue();
        _world.NotifyByTelegram.Should().BeTrue();
        _world.LastText.Should().StartWith("Notifications are on.");

        await _world.Handler().HandleAsync(Press("nt:off"), CancellationToken.None);

        _world.ProfilePuts.Last().GetProperty("notifyByTelegram").GetBoolean().Should().BeFalse();
        _world.NotifyByTelegram.Should().BeFalse();
        _world.LastText.Should().StartWith("Notifications are off.");
    }

    [Fact]
    public async Task NotificationsWithAnythingElse_ExplainsTheWords_AndChangesNothing()
    {
        await _world.Handler().HandleAsync(Text("/notifications loud"), CancellationToken.None);

        _world.LastText.Should().StartWith("Send <code>/notifications on</code> or <code>/notifications off</code> for this chat, or a kind and on or off");
        _world.ProfilePuts.Should().BeEmpty();
        _world.PreferencePuts.Should().BeEmpty();
    }

    [Fact]
    public async Task Notifications_IsAProFeature_LikeTheRestOfTheBot()
    {
        _world.IsPro = false;

        await _world.Handler().HandleAsync(Text("/notifications on"), CancellationToken.None);

        _world.LastText.Should().StartWith("The Telegram bot is part of Mizan Pro.");
        _world.ProfilePuts.Should().BeEmpty();
    }

    [Fact]
    public async Task Help_ListsNotifications()
    {
        await _world.Handler().HandleAsync(Text("/help"), CancellationToken.None);

        _world.LastText.Should().Contain("<b>/notifications</b>");
    }
}
