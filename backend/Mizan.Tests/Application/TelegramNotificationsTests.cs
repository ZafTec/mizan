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
    public async Task Notifications_SaysWhetherTheyComeHere_WithOneButtonToFlipIt()
    {
        await _world.Handler().HandleAsync(Text("/notifications"), CancellationToken.None);

        _world.LastText.Should().Be("Notifications are <b>off</b>: they stay in the app.");
        _world.LastKeyboard().Should().Equal(("Turn on", "nt:on"));
        _world.ProfilePuts.Should().BeEmpty();

        _world.NotifyByTelegram = true;
        await _world.Handler().HandleAsync(Text("/notifications"), CancellationToken.None);

        _world.LastText.Should().StartWith("Notifications are <b>on</b>");
        _world.LastKeyboard().Should().Equal(("Turn off", "nt:off"));
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
    public async Task NotificationsWithAnythingElse_ExplainsTheTwoWords_AndChangesNothing()
    {
        await _world.Handler().HandleAsync(Text("/notifications loud"), CancellationToken.None);

        _world.LastText.Should().Be("Send <code>/notifications on</code> or <code>/notifications off</code>.");
        _world.ProfilePuts.Should().BeEmpty();
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
