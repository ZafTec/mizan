using FluentAssertions;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>
/// The bot's internal send, at its two wires: what the API's outbox posts, and what reaches Telegram.
/// Only a caller holding the service key gets a message sent.
/// </summary>
public class TelegramNotifyEndpointTests
{
    private readonly TelegramFakeWorld _world = new();

    private const string Reminder = """{ "telegramUserId": 42, "text": "Your window closes at 20:00. Today: 1,640 of 1,800 kcal.", "openPath": "plan?planId=55555555-5555-4555-8555-555555555555&day=2026-10-22" }""";

    [Fact]
    public async Task TheApisReminder_ReachesTheLinkedChat_WithAnOpenButtonToThePlanDay()
    {
        (await _world.PostNotifyAsync(Reminder, "key")).Should().Be(204);

        _world.LastSend.GetProperty("chat_id").GetInt64().Should().Be(42);
        _world.LastText.Should().Be("Your window closes at 20:00. Today: 1,640 of 1,800 kcal.");
        _world.LastWebAppButtons().Should().Equal(("Open", "https://mizan.test/tg/plan?planId=55555555-5555-4555-8555-555555555555&day=2026-10-22"));
    }

    [Fact]
    public async Task WithoutTheServiceKey_NothingIsSent()
    {
        (await _world.PostNotifyAsync(Reminder, null)).Should().Be(401);
        (await _world.PostNotifyAsync(Reminder, "kez")).Should().Be(401);
        (await _world.PostNotifyAsync("""{ "telegramUserId": 42 }""", "key")).Should().Be(400);

        _world.MessagesSent.Should().Be(0);
    }
}
