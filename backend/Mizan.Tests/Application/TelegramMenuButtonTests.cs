extern alias telegram;
using FluentAssertions;
using Mizan.Contracts.Telegram;
using telegram::Mizan.Telegram.Bot;
using Xunit;
using static Mizan.Tests.Application.TelegramFakeWorld;

namespace Mizan.Tests.Application;

/// <summary>
/// The bot opens the Mini App from the button beside the chat input, and every button it sends points at a page
/// the Mini App serves.
/// </summary>
public class TelegramMenuButtonTests
{
    private readonly TelegramFakeWorld _world = new();

    [Fact]
    public async Task AtStartup_TheMenuButtonOpensTheMiniAppHome_OverHttps()
    {
        await BotProfile.ApplyAsync(_world.Telegram(), _world.BotOptions.Value, CancellationToken.None);

        _world.TelegramCalls.Select(c => c.Method).Should().Contain("setMyCommands");
        var button = _world.TelegramCalls.Should().ContainSingle(c => c.Method == "setChatMenuButton").Subject.Body.GetProperty("menu_button");
        button.GetProperty("type").GetString().Should().Be("web_app");
        button.GetProperty("text").GetString().Should().Be("Open Mizan");
        button.GetProperty("web_app").GetProperty("url").GetString().Should().Be("https://mizan.test/tg");
    }

    [Fact]
    public async Task WithoutHttps_TheMenuButtonIsTheCommandsMenu()
    {
        _world.BotOptions.Value.PublicUrl = "http://localhost:3000";

        await BotProfile.ApplyAsync(_world.Telegram(), _world.BotOptions.Value, CancellationToken.None);

        var button = _world.TelegramCalls.Should().ContainSingle(c => c.Method == "setChatMenuButton").Subject.Body.GetProperty("menu_button");
        button.GetProperty("type").GetString().Should().Be("commands");
        button.TryGetProperty("web_app", out _).Should().BeFalse();
    }

    [Fact]
    public async Task EveryMiniAppButtonTheBotSends_OpensAPageTheMiniAppServes()
    {
        var handler = _world.Handler();
        foreach (var text in new[] { "/start abc123", "/help", "/plan", "/shop" })
            await handler.HandleAsync(Text(text), CancellationToken.None);

        var buttons = _world.AllWebAppButtons();
        buttons.Should().HaveCountGreaterThanOrEqualTo(4);
        const string home = "https://mizan.test/tg";
        buttons.Should().AllSatisfy(b =>
        {
            b.Url.Should().StartWith(home);
            MiniAppPages.All.Should().Contain(MiniAppPages.PageOf(b.Url[home.Length..]), b.Url);
        });
    }
}
