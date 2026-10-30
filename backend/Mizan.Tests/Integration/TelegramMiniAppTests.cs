using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Auth;
using Mizan.Contracts.Telegram;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The API's half of the Mini App sign-in: the bot, having checked Telegram's signature, asks for a session for a
/// Telegram id. The answer has to be an ordinary session for the linked account, and nothing for anyone else.
/// </summary>
[Collection("ApiIntegration")]
public class TelegramMiniAppTests
{
    private const long ChatId = 424242;

    private readonly ApiTestFixture _fixture;

    public TelegramMiniAppTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ALinkedProUser_GetsASession_ThatSignsTheBrowserIn_ForADay()
    {
        var userId = await LinkedUserAsync(pro: true);

        var before = DateTime.UtcNow;
        var response = await Bot().PostAsJsonAsync("/api/Telegram/sessions", new TelegramMiniAppSessionRequest(ChatId, "203.0.113.9", "Telegram-Android/11.0"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = (await response.Content.ReadFromJsonAsync<TelegramMiniAppSessionResponse>())!;
        session.UserId.Should().Be(userId);
        session.Token.Should().NotBeNullOrWhiteSpace();
        session.ExpiresAt.Should().BeCloseTo(before.AddHours(24), TimeSpan.FromMinutes(1), "a Mini App session lasts a day, not a week");

        // The token is a plain mizan_session cookie: GET /api/Auth/me accepts it like any sign-in.
        using var page = _fixture.CreateClient();
        page.DefaultRequestHeaders.Add("Cookie", $"{ApiTestFixture.SessionCookieName}={session.Token}");
        var me = await page.GetAsync("/api/Auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        (await me.Content.ReadFromJsonAsync<AuthUserDto>())!.Id.Should().Be(userId);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var row = await db.UserSessions.SingleAsync(s => s.UserId == userId);
        row.ImpersonatorId.Should().BeNull("it is the user's own session");
        row.UserAgent.Should().Be("Telegram-Android/11.0");
        row.IpAddress.Should().Be("203.0.113.9");
        (row.ExpiresAt - row.LastSeenAt).Should().BeCloseTo(TimeSpan.FromHours(24), TimeSpan.FromSeconds(5));

        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "CreateTelegramMiniAppSessionCommand");
        audit.Details.Should().Contain(ChatId.ToString()).And.NotContain(session.Token);
    }

    [Fact]
    public async Task AChatNobodyLinked_GetsNothing()
    {
        await _fixture.ResetDatabaseAsync();

        var response = await Bot().PostAsJsonAsync("/api/Telegram/sessions", new TelegramMiniAppSessionRequest(ChatId));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("not_found");
    }

    [Fact]
    public async Task AFreeAccount_IsSentToUpgrade_LikeTheRestOfTheBot()
    {
        var userId = await LinkedUserAsync(pro: false);

        var response = await Bot().PostAsJsonAsync("/api/Telegram/sessions", new TelegramMiniAppSessionRequest(ChatId));

        response.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        (await response.Content.ReadAsStringAsync()).Should().Contain("upgrade_required");

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        (await db.UserSessions.AnyAsync(s => s.UserId == userId)).Should().BeFalse("no session is made for a refused sign-in");
    }

    [Fact]
    public async Task ABannedAccount_CannotSignInThroughTelegramEither()
    {
        var userId = await LinkedUserAsync(pro: true, banned: true);

        var response = await Bot().PostAsJsonAsync("/api/Telegram/sessions", new TelegramMiniAppSessionRequest(ChatId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        (await db.UserSessions.AnyAsync(s => s.UserId == userId)).Should().BeFalse();
    }

    [Fact]
    public async Task OnlyTheBotsServiceKey_MayAskForOne()
    {
        var userId = await LinkedUserAsync(pro: true);
        var body = new TelegramMiniAppSessionRequest(ChatId);

        // A signed-in person is not the bot: knowing a chat id must not turn into a session for its owner.
        using var user = _fixture.CreateAuthenticatedClient(userId, $"tg-{userId:N}@example.com");
        (await user.PostAsJsonAsync("/api/Telegram/sessions", body)).StatusCode
            .Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);

        using var anonymous = _fixture.CreateClient();
        (await anonymous.PostAsJsonAsync("/api/Telegram/sessions", body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var wrongKey = _fixture.CreateClient();
        wrongKey.DefaultRequestHeaders.Add("X-Api-Key", "not-the-key");
        (await wrongKey.PostAsJsonAsync("/api/Telegram/sessions", body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- Helpers ----------------------------------------------------------

    private async Task<Guid> LinkedUserAsync(bool pro, bool banned = false)
    {
        await _fixture.ResetDatabaseAsync();

        var id = Guid.NewGuid();
        await _fixture.SeedUserAsync(id, $"tg-{id:N}@example.com", banned: banned);
        if (pro) await _fixture.GrantProAsync(id);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        db.TelegramLinks.Add(new TelegramLink
        {
            Id = Guid.CreateVersion7(),
            UserId = id,
            TelegramUserId = ChatId,
            TelegramUsername = "tester",
            LinkedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return id;
    }

    /// <summary>The bot: a service key and no session.</summary>
    private HttpClient Bot()
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
        return client;
    }
}
