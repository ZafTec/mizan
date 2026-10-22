extern alias telegram;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>
/// The Mini App's sign-in at its two wires: the initData the page sends, and the session the API answers with.
/// What matters is that only Telegram's own signature gets through, and that the page ends up signed in.
/// </summary>
public class TelegramMiniAppEndpointTests
{
    private readonly TelegramFakeWorld _world = new();

    private static string Body(string initData) => JsonSerializer.Serialize(new { initData });

    [Fact]
    public async Task ASignedFreshInitData_BecomesASession_AndTheCookieThatCarriesIt()
    {
        var (status, body, cookie) = await _world.PostMiniAppSessionAsync(Body(SignedInitData.Valid()));

        status.Should().Be(200);
        body.GetProperty("token").GetString().Should().Be(TelegramFakeWorld.SessionToken);
        body.GetProperty("expiresAt").GetDateTime().ToUniversalTime().Should().Be(TelegramFakeWorld.SessionExpiresAt);

        // The same attributes the API puts on its own sign-in cookie, so the page is signed in at once.
        cookie.Should().NotBeNull();
        cookie.Should().StartWith($"mizan_session={TelegramFakeWorld.SessionToken};");
        cookie.Should().ContainEquivalentOf("expires=Fri, 23 Oct 2026 10:25:00 GMT");
        cookie.Should().ContainEquivalentOf("path=/");
        cookie.Should().ContainEquivalentOf("secure");
        cookie.Should().ContainEquivalentOf("samesite=lax");
        cookie.Should().ContainEquivalentOf("httponly");

        // The API was asked for exactly the user Telegram signed, with the WebView's details for the sessions screen.
        var asked = _world.SessionsPosted.Should().ContainSingle().Subject;
        asked.GetProperty("telegramUserId").GetInt64().Should().Be(42);
        asked.GetProperty("userAgent").GetString().Should().Be("Telegram-Android/11.0");
        asked.GetProperty("ipAddress").GetString().Should().Be("203.0.113.9");
    }

    [Fact]
    public async Task InDevelopment_TheCookieIsNotMarkedSecure_SoPlainHttpWorks()
    {
        _world.EnvironmentName = "Development";

        var (status, _, cookie) = await _world.PostMiniAppSessionAsync(Body(SignedInitData.Valid()));

        status.Should().Be(200);
        cookie.Should().NotContainEquivalentOf("secure").And.ContainEquivalentOf("httponly");
    }

    [Theory]
    [InlineData("tampered")]
    [InlineData("other-bot")]
    [InlineData("stale")]
    [InlineData("no-hash")]
    public async Task AnythingButTelegramsOwnFreshSignature_IsRefused_AndTheApiIsNeverAsked(string kind)
    {
        var initData = kind switch
        {
            "tampered" => SignedInitData.Valid().Replace("%22id%22%3A42", "%22id%22%3A43"),
            "other-bot" => SignedInitData.Valid(token: "999999:SOME-OTHER-BOT"),
            "stale" => SignedInitData.Valid(authDate: SignedInitData.Now.AddMinutes(-6)),
            _ => "query_id=abc&user=%7B%22id%22%3A42%7D&auth_date=1761130800",
        };

        var (status, body, cookie) = await _world.PostMiniAppSessionAsync(Body(initData));

        status.Should().Be(401);
        body.GetProperty("errorCode").GetString().Should().Be("invalid_init_data");
        body.GetProperty("error").GetString().Should().NotBeNullOrEmpty();
        cookie.Should().BeNull();
        _world.SessionsPosted.Should().BeEmpty("an unverified claim never reaches the API");
    }

    [Fact]
    public async Task TheWindowFollowsTheBotsClock_NotTheTestersWatch()
    {
        // The same statement is fresh at one moment and stale six minutes later.
        var initData = SignedInitData.Valid();
        (await _world.PostMiniAppSessionAsync(Body(initData))).Status.Should().Be(200);

        _world.Now = SignedInitData.Now.AddMinutes(6);
        (await _world.PostMiniAppSessionAsync(Body(initData))).Status.Should().Be(401);
    }

    [Fact]
    public async Task AChatThatIsNotLinked_GetsTheSettingsPage()
    {
        _world.Linked = false;

        var (status, body, cookie) = await _world.PostMiniAppSessionAsync(Body(SignedInitData.Valid()));

        status.Should().Be(404);
        body.GetProperty("errorCode").GetString().Should().Be("not_linked");
        body.GetProperty("linkUrl").GetString().Should().Be("https://mizan.test/profile/settings/telegram");
        cookie.Should().BeNull();
    }

    [Fact]
    public async Task AFreeAccount_GetsTheUpgradePath_AsEverywhereElseInTheBot()
    {
        _world.IsPro = false;

        var (status, body, cookie) = await _world.PostMiniAppSessionAsync(Body(SignedInitData.Valid()));

        status.Should().Be(402);
        body.GetProperty("errorCode").GetString().Should().Be("upgrade_required");
        body.GetProperty("upgradeUrl").GetString().Should().Be("https://mizan.test/billing");
        cookie.Should().BeNull();
    }

    [Fact]
    public async Task AnAccountThatCannotSignIn_IsToldSo_WithTheApisReason()
    {
        _world.SessionStatus = HttpStatusCode.Forbidden;

        var (status, body, cookie) = await _world.PostMiniAppSessionAsync(Body(SignedInitData.Valid()));

        status.Should().Be(403);
        body.GetProperty("errorCode").GetString().Should().Be("forbidden");
        body.GetProperty("error").GetString().Should().Be("This account is suspended.");
        cookie.Should().BeNull();
    }

    [Theory]
    [InlineData("", "application/json")]
    [InlineData("not json", "application/json")]
    [InlineData("""{"initData":"x"}""", "text/plain")]
    public async Task ABodyThatIsNotTheRequest_IsABadRequest(string body, string contentType)
    {
        var (status, parsed, _) = await _world.PostMiniAppSessionAsync(body, contentType);

        status.Should().Be(400);
        parsed.GetProperty("errorCode").GetString().Should().Be("bad_request");
    }
}
