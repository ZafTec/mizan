extern alias telegram;
using FluentAssertions;
using Mizan.Contracts.Notifications;
using Mizan.Contracts.Telegram;
using telegram::Mizan.Telegram;
using telegram::Mizan.Telegram.Bot;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>How the bot renders a notification card: HTML, picture or text, the caps, and which buttons survive.</summary>
public class NotificationCardsTests
{
    private static readonly TelegramBotOptions Https = new() { PublicUrl = "https://mizan.test" };
    private static readonly TelegramBotOptions Local = new() { PublicUrl = "http://localhost:3000" };

    public static TheoryData<string, TelegramNotifyCard, bool, string, string?> Cards => new()
    {
        {
            "title, body and facts",
            Card("Achievement unlocked: First Meal", "Log your first meal", facts: [new("Points", "+10"), new("Category", "Nutrition")]),
            true,
            "<b>Achievement unlocked: First Meal</b>\nLog your first meal\n\nPoints: <b>+10</b>\nCategory: <b>Nutrition</b>",
            null
        },
        {
            "markup in the words is escaped",
            Card("Tom & Jerry <3", "a < b"),
            true,
            "<b>Tom &amp; Jerry &lt;3</b>\na &lt; b",
            null
        },
        {
            "an https picture makes it a caption",
            Card("Streak", null, image: "https://mizan.test/badges/512/achievement-gold-badge.png"),
            true,
            "<b>Streak</b>",
            "https://mizan.test/badges/512/achievement-gold-badge.png"
        },
        {
            "a picture Telegram cannot fetch is left out",
            Card("Streak", null, image: "http://localhost:3000/badges/512/achievement-gold-badge.png"),
            true,
            "<b>Streak</b>",
            null
        },
        {
            "the text fallback drops the picture",
            Card("Streak", null, image: "https://mizan.test/badge.png"),
            false,
            "<b>Streak</b>",
            null
        },
    };

    [Theory]
    [MemberData(nameof(Cards))]
    public void RendersTheCard(string because, TelegramNotifyCard card, bool asPhoto, string html, string? photo)
    {
        var rendered = NotificationCards.Render(new TelegramNotifyRequest(1, "plain", null, card), Https, asPhoto);

        rendered.Html.Should().Be(html, because);
        rendered.PhotoUrl.Should().Be(photo, because);
    }

    [Theory]
    [InlineData("https://mizan.test/badge.png", NotificationCards.CaptionLimit)]
    [InlineData(null, NotificationCards.TextLimit)]
    public void ALongBodyIsShortenedToTheCap_KeepingTheTitleAndFacts(string? image, int limit)
    {
        var body = string.Join(' ', Enumerable.Repeat("word & more", 800));
        var rendered = NotificationCards.Render(
            new TelegramNotifyRequest(1, "plain", null, Card("Title", body, image: image, facts: [new("Today", "1,640 kcal")])), Https);

        rendered.Html.Length.Should().BeLessThanOrEqualTo(limit);
        rendered.Html.Should().StartWith("<b>Title</b>\nword &amp; more").And.EndWith("…\n\nToday: <b>1,640 kcal</b>");
    }

    public static TheoryData<string, TelegramBotOptions, TelegramNotifyButton, string?, string?> Buttons => new()
    {
        { "a Mini App page opens in the Mini App", Https, new("Open plan", "plan?planId=1", "/meal-plan/1"), "https://mizan.test/tg/plan?planId=1", null },
        { "a page the Mini App lacks opens on the web", Https, new("See achievements", null, "/achievements"), null, "https://mizan.test/achievements" },
        { "nothing over plain http", Local, new("Open plan", "plan?planId=1", "/meal-plan/1"), null, null },
    };

    [Theory]
    [MemberData(nameof(Buttons))]
    public void ButtonsOpenTheMiniAppOrTheWeb_OrAreLeftOut(string because, TelegramBotOptions options, TelegramNotifyButton button, string? webApp, string? url)
    {
        var rendered = NotificationCards.Render(new TelegramNotifyRequest(1, "plain", null, Card("T", null, buttons: [button])), options);

        var rendered_ = rendered.Keyboard?.InlineKeyboard.SelectMany(r => r).SingleOrDefault();
        if (webApp is null && url is null)
        {
            rendered.Keyboard.Should().BeNull(because);
            return;
        }
        rendered_!.Text.Should().Be(button.Label, because);
        rendered_.WebApp?.Url.Should().Be(webApp, because);
        rendered_.Url.Should().Be(url, because);
    }

    [Fact]
    public void ARequestWithoutACard_IsItsTextWithAnOpenButton()
    {
        var rendered = NotificationCards.Render(new TelegramNotifyRequest(1, "Eating window\nCloses at 20:00 & soon", "plan"), Https);

        rendered.Html.Should().Be("Eating window\nCloses at 20:00 &amp; soon");
        rendered.PhotoUrl.Should().BeNull();
        rendered.Keyboard!.InlineKeyboard.Single().Single().WebApp!.Url.Should().Be("https://mizan.test/tg/plan");
    }

    private static TelegramNotifyCard Card(
        string title,
        string? body,
        string? image = null,
        IReadOnlyList<NotificationFact>? facts = null,
        IReadOnlyList<TelegramNotifyButton>? buttons = null) =>
        new(title, body, image, buttons ?? [], facts ?? []);
}
