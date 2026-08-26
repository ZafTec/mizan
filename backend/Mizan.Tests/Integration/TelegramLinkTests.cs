using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Telegram;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// Account linking, which is the part of the bot worth testing.
///
/// A Telegram chat id is not an identity. Everything here is one question in
/// different shapes: can a party that knows only a chat id get at somebody's
/// account? The answer has to stay no, whatever it knows.
/// </summary>
[Collection("ApiIntegration")]
public class TelegramLinkTests
{
    private const long ChatId = 987654321;

    private readonly ApiTestFixture _fixture;

    public TelegramLinkTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ACodeIssuedOnTheWebLinksTheChatThatSpendsIt()
    {
        var (userId, user) = await SignedInAsync();
        using var _ = user;

        var code = await IssueAsync(user);

        var linked = await Bot().PostAsJsonAsync("/api/Telegram/resolve", new
        {
            code,
            telegramUserId = ChatId,
            telegramUsername = "@someone",
        });

        linked.StatusCode.Should().Be(HttpStatusCode.OK);
        (await linked.Content.ReadFromJsonAsync<TelegramLinkResult>())!.UserId.Should().Be(userId);

        var resolved = await Bot().GetFromJsonAsync<ResolvedTelegramUser>($"/api/Telegram/resolve/{ChatId}");
        resolved!.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task ACodeIsSpentOnce()
    {
        var (_, user) = await SignedInAsync();
        using var _u = user;

        var code = await IssueAsync(user);

        (await LinkAsync(code, ChatId)).StatusCode.Should().Be(HttpStatusCode.OK);

        // A second chat replaying the same deep link is the attack this
        // guards: the link is shareable, the code is not reusable.
        (await LinkAsync(code, ChatId + 1)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await Bot().GetAsync($"/api/Telegram/resolve/{ChatId + 1}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AnExpiredCodeIsRefused()
    {
        var (userId, user) = await SignedInAsync();
        using var _ = user;

        var code = await IssueAsync(user);
        await ExpireAsync(userId);

        (await LinkAsync(code, ChatId)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task IssuingASecondCodeInvalidatesTheFirst()
    {
        var (_, user) = await SignedInAsync();
        using var _u = user;

        var first = await IssueAsync(user);
        var second = await IssueAsync(user);

        first.Should().NotBe(second);

        (await LinkAsync(first, ChatId)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LinkAsync(second, ChatId)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnUnknownChatResolvesToNothing()
    {
        await _fixture.ResetDatabaseAsync();

        (await Bot().GetAsync("/api/Telegram/resolve/1122334455")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ResolvingIsRefusedWithoutTheServiceKey()
    {
        var (_, user) = await SignedInAsync();
        using var _u = user;

        // A signed-in browser session is not the bot. Nobody gets to turn a
        // chat id into a user id by being logged in.
        (await user.GetAsync($"/api/Telegram/resolve/{ChatId}")).StatusCode
            .Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task IssuingACodeRequiresBeingSignedIn()
    {
        await _fixture.ResetDatabaseAsync();
        using var anonymous = _fixture.CreateClient();

        (await anonymous.PostAsync("/api/Telegram/link", null)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RelinkingReplacesBothSidesRatherThanAccumulating()
    {
        var (firstUserId, first) = await SignedInAsync();
        using var _f = first;

        await LinkAsync(await IssueAsync(first), ChatId);

        // Same phone, different account - a shared device, or somebody with a
        // second profile. The chat must end up on exactly one of them.
        var (secondUserId, second) = await SignedInAsync(reset: false);
        using var _s = second;

        (await LinkAsync(await IssueAsync(second), ChatId)).StatusCode.Should().Be(HttpStatusCode.OK);

        var resolved = await Bot().GetFromJsonAsync<ResolvedTelegramUser>($"/api/Telegram/resolve/{ChatId}");
        resolved!.UserId.Should().Be(secondUserId);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        (await db.TelegramLinks.CountAsync()).Should().Be(1);
        (await db.TelegramLinks.AnyAsync(l => l.UserId == firstUserId)).Should().BeFalse();
        secondUserId.Should().NotBe(firstUserId);
    }

    [Fact]
    public async Task TheUserCanSeeAndBreakTheirOwnLink()
    {
        var (_, user) = await SignedInAsync();
        using var _u = user;

        (await user.GetFromJsonAsync<TelegramLinkDto>("/api/Telegram/link"))!.Linked.Should().BeFalse();

        await LinkAsync(await IssueAsync(user), ChatId);

        var linked = await user.GetFromJsonAsync<TelegramLinkDto>("/api/Telegram/link");
        linked!.Linked.Should().BeTrue();
        linked.TelegramUsername.Should().Be("someone", "the @ is display noise");

        (await user.DeleteAsync("/api/Telegram/link")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Bot().GetAsync($"/api/Telegram/resolve/{ChatId}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TheChatCanBreakTheLinkToo()
    {
        var (_, user) = await SignedInAsync();
        using var _u = user;

        await LinkAsync(await IssueAsync(user), ChatId);

        // Unlinking has to work from whichever device you still have.
        (await Bot().DeleteAsync($"/api/Telegram/resolve/{ChatId}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        (await user.GetFromJsonAsync<TelegramLinkDto>("/api/Telegram/link"))!.Linked.Should().BeFalse();
    }

    // ---- Helpers ----------------------------------------------------------

    private async Task<(Guid Id, HttpClient Client)> SignedInAsync(bool reset = true)
    {
        if (reset) await _fixture.ResetDatabaseAsync();

        var id = Guid.NewGuid();
        var email = $"tg-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email);
        return (id, _fixture.CreateAuthenticatedClient(id, email));
    }

    /// <summary>The bot: a service key and no session.</summary>
    private HttpClient Bot()
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
        return client;
    }

    private static async Task<string> IssueAsync(HttpClient user)
    {
        var response = await user.PostAsync("/api/Telegram/link", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<TelegramLinkCodeDto>();
        dto!.DeepLink.Should().Contain(dto.Code);
        return dto.Code;
    }

    private Task<HttpResponseMessage> LinkAsync(string code, long telegramUserId) =>
        Bot().PostAsJsonAsync("/api/Telegram/resolve", new
        {
            code,
            telegramUserId,
            telegramUsername = "@someone",
        });

    private async Task ExpireAsync(Guid userId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

        await db.UserTokens
            .Where(t => t.UserId == userId && t.Purpose == UserTokenPurpose.TelegramLink)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
    }
}
