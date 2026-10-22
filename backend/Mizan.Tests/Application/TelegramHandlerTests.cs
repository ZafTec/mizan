extern alias telegram;
using FluentAssertions;
using Xunit;
using static Mizan.Tests.Application.TelegramFakeWorld;

namespace Mizan.Tests.Application;

/// <summary>
/// The bot at its two wires: what a person types, and what the API answers. Nothing in between is asserted.
/// </summary>
public class TelegramHandlerTests
{
    private readonly TelegramFakeWorld _world = new();

    [Fact]
    public async Task Today_RendersTheDaysTotalsAgainstTargets_BoldingOnlyTheNumbers()
    {
        await _world.Handler().HandleAsync(Text("/today"), CancellationToken.None);

        _world.LastText.Should().Be(
            "<b>Today</b>\n"
            + "Calories: <b>1860</b> / 2000 kcal (93%)\n"
            + "Protein: <b>120</b> / 150 g (80%)\n"
            + "Carbs: <b>200</b> g\n"
            + "Fat: <b>60</b> g\n");
        _world.LastSend.GetProperty("parse_mode").GetString().Should().Be("HTML");
    }

    [Fact]
    public async Task FreeText_ContinuesTheNewestThread_AndSendsTheReplyAsHtml()
    {
        _world.ThreadsJson = """[{"id":"44444444-4444-4444-8444-444444444444","title":"Earlier","updatedAt":"2026-10-21T00:00:00Z"}]""";
        _world.ChatReply = "You are at **120 g** protein.";

        await _world.Handler().HandleAsync(Text("how is my protein?"), CancellationToken.None);

        var posted = _world.ChatPosted.Should().ContainSingle().Subject;
        posted.GetProperty("threadId").GetGuid().Should().Be(Guid.Parse("44444444-4444-4444-8444-444444444444"));
        posted.GetProperty("message").GetString().Should().Be("how is my protein?");
        _world.LastText.Should().Be("You are at <b>120 g</b> protein.");
        _world.LastSend.GetProperty("parse_mode").GetString().Should().Be("HTML");
    }

    [Fact]
    public async Task FreeText_WithNoThreadYet_StartsOne()
    {
        await _world.Handler().HandleAsync(Text("hello"), CancellationToken.None);

        _world.ChatPosted.Should().ContainSingle().Which.GetProperty("threadId").ValueKind
            .Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task WhatTheAssistantDid_IsListedUnderItsReply()
    {
        _world.ChatReply = "Done.";
        _world.PerformedJson = """
            [{"tool":"create_shopping_list","summary":"Created shopping list \"Week 43\"","succeeded":true,"error":null},
             {"tool":"add_shopping_list_item","summary":"Add 2 kg <teff>","succeeded":false,"error":"List is full"}]
            """;

        await _world.Handler().HandleAsync(Text("make me a list"), CancellationToken.None);

        _world.LastText.Should().Be(
            "Done.\n"
            + "\n✓ Created shopping list \"Week 43\""
            + "\n✗ Add 2 kg &lt;teff&gt;: List is full");
    }

    [Fact]
    public async Task StartWithACode_LinksTheChat_AndGreetsByName()
    {
        await _world.Handler().HandleAsync(Text("/start abc123"), CancellationToken.None);

        var link = _world.LinkPosted.Should().ContainSingle().Subject;
        link.GetProperty("code").GetString().Should().Be("abc123");
        link.GetProperty("telegramUserId").GetInt64().Should().Be(42);
        link.GetProperty("telegramUsername").GetString().Should().Be("tester");
        _world.LastText.Should().StartWith("Connected. Hello Tester.").And.Contain("/today");
    }

    [Fact]
    public async Task StartWithABadCode_RepeatsTheApisReason_AndPointsAtSettings()
    {
        _world.LinkProblem = "That code has expired.";

        await _world.Handler().HandleAsync(Text("/start stale"), CancellationToken.None);

        _world.LastText.Should().StartWith("That code has expired.")
            .And.Contain("https://mizan.test/profile/settings/telegram");
    }

    [Fact]
    public async Task AnUnlinkedChat_IsSentToSignIn_NotToTheAssistant()
    {
        _world.Linked = false;

        await _world.Handler().HandleAsync(Text("hello"), CancellationToken.None);

        _world.ChatPosted.Should().BeEmpty();
        _world.LastText.Should().Contain("not connected");
    }
}
