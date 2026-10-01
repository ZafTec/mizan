using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The authorization server, driven end to end the way an MCP client drives it:
/// register, authorize, consent in the browser, exchange the code, refresh.
/// </summary>
[Collection("ApiIntegration")]
public class OAuthFlowTests
{
    private const string Redirect = "http://localhost:8123/callback";
    private readonly ApiTestFixture _fixture;

    public OAuthFlowTests(ApiTestFixture fixture) => _fixture = fixture;

    // ---- discovery and CORS ----

    [Theory]
    [InlineData("/api/.well-known/openid-configuration")]
    [InlineData("/api/.well-known/oauth-authorization-server")]
    public async Task Discovery_AdvertisesEndpointsAndPkce(string path)
    {
        using var client = _fixture.CreateClient();
        var doc = await client.GetFromJsonAsync<JsonElement>(path);

        doc.GetProperty("issuer").GetString().Should().Be("http://localhost/api");
        doc.GetProperty("authorization_endpoint").GetString().Should().Be("http://localhost/api/oauth/authorize");
        doc.GetProperty("token_endpoint").GetString().Should().Be("http://localhost/api/oauth/token");
        doc.GetProperty("registration_endpoint").GetString().Should().Be("http://localhost/api/oauth/register");
        doc.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(e => e.GetString())
            .Should().BeEquivalentTo(new[] { "S256" });
        doc.GetProperty("scopes_supported").EnumerateArray().Select(e => e.GetString())
            .Should().Contain("nutrition:read").And.NotContain("admin");
    }

    [Fact]
    public async Task PublicEndpoints_AllowAnyOrigin_ButTheConsentEndpointsDoNot()
    {
        using var client = _fixture.CreateClient();

        var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/oauth/token");
        preflight.Headers.Add("Origin", "https://inspector.example");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");
        var response = await client.SendAsync(preflight);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Which.Should().Be("*");

        var consent = new HttpRequestMessage(HttpMethod.Options, "/api/oauth/authorization-requests/decision");
        consent.Headers.Add("Origin", "https://inspector.example");
        consent.Headers.Add("Access-Control-Request-Method", "POST");
        var consentResponse = await client.SendAsync(consent);
        consentResponse.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    // ---- the happy path ----

    [Fact]
    public async Task FullFlow_IssuesTokensThatIntrospectWithTheGrantedScopes()
    {
        var (user, browser) = await SignedInAsync();
        var clientId = await RegisterAsync();
        var pkce = Pkce.New();

        var secret = await AuthorizeAsync(clientId, pkce, scope: "nutrition:write training:read admin");

        var view = await browser.PostAsJsonAsync("/api/oauth/authorization-requests/view", new { request = secret });
        view.StatusCode.Should().Be(HttpStatusCode.OK);
        var shown = await view.Content.ReadFromJsonAsync<JsonElement>();
        shown.GetProperty("clientName").GetString().Should().Be("Test Assistant");
        shown.GetProperty("source").GetString().Should().Be("dynamic");
        shown.GetProperty("scopeGroups").EnumerateArray().Select(g => g.GetProperty("group").GetString())
            .Should().Contain("nutrition").And.NotContain("admin", "a plain user is never offered admin");

        var code = await ApproveAsync(browser, secret, new[] { "nutrition:write", "training:read", "admin" });
        var tokens = await ExchangeAsync(clientId, code, pkce.Verifier);

        tokens.GetProperty("token_type").GetString().Should().Be("Bearer");
        tokens.GetProperty("access_token").GetString().Should().StartWith("mza_");
        tokens.GetProperty("refresh_token").GetString().Should().StartWith("mzr_");
        tokens.GetProperty("expires_in").GetInt32().Should().Be(3600);

        var info = await IntrospectAsync(tokens.GetProperty("access_token").GetString()!);
        info.GetProperty("active").GetBoolean().Should().BeTrue();
        info.GetProperty("userId").GetGuid().Should().Be(user);
        info.GetProperty("scopes").EnumerateArray().Select(e => e.GetString())
            .Should().BeEquivalentTo(new[] { "nutrition:read", "nutrition:write", "training:read" },
                "write implies read, and admin was dropped because the user is not an administrator");
        info.GetProperty("householdMode").GetString().Should().Be("none");
    }

    [Fact]
    public async Task AnAdminMayGrantAdmin_AndLosesItWhenDemoted()
    {
        var (user, browser) = await SignedInAsync(role: "admin");
        var clientId = await RegisterAsync();
        var pkce = Pkce.New();
        var secret = await AuthorizeAsync(clientId, pkce, scope: "profile:read admin");
        var code = await ApproveAsync(browser, secret, new[] { "profile:read", "admin" });
        var tokens = await ExchangeAsync(clientId, code, pkce.Verifier);
        var access = tokens.GetProperty("access_token").GetString()!;

        (await IntrospectAsync(access)).GetProperty("scopes").EnumerateArray().Select(e => e.GetString())
            .Should().Contain("admin");

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
            await db.Users.Where(u => u.Id == user).ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, "user"));
            await scope.ServiceProvider.GetRequiredService<IUserCacheInvalidator>().InvalidateAsync(user);
        }

        (await IntrospectAsync(access)).GetProperty("scopes").EnumerateArray().Select(e => e.GetString())
            .Should().NotContain("admin", "the token reads the account's role now, not at consent time");
    }

    [Fact]
    public async Task SelectedHouseholds_KeepsOnlyOnesTheUserBelongsTo()
    {
        var (user, browser) = await SignedInAsync();
        var mine = await SeedHouseholdAsync(user);
        var someoneElses = await SeedHouseholdAsync(await SeedOtherUserAsync());

        var clientId = await RegisterAsync();
        var pkce = Pkce.New();
        var secret = await AuthorizeAsync(clientId, pkce, scope: "planning:write");
        var code = await ApproveAsync(browser, secret, new[] { "planning:write" }, "selected", new[] { mine, someoneElses });
        var tokens = await ExchangeAsync(clientId, code, pkce.Verifier);

        var info = await IntrospectAsync(tokens.GetProperty("access_token").GetString()!);
        info.GetProperty("householdMode").GetString().Should().Be("selected");
        info.GetProperty("householdIds").EnumerateArray().Select(e => e.GetGuid()).Should().BeEquivalentTo(new[] { mine });
    }

    // ---- hostile or broken requests ----

    [Fact]
    public async Task Token_RejectsAWrongPkceVerifier_AndTheCodeStaysUsableForTheRightOne()
    {
        var (_, browser) = await SignedInAsync();
        var clientId = await RegisterAsync();
        var pkce = Pkce.New();
        var code = await ApproveAsync(browser, await AuthorizeAsync(clientId, pkce, "profile:read"), new[] { "profile:read" });

        var bad = await TokenAsync(clientId, code, Pkce.New().Verifier);
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await bad.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().Should().Be("invalid_grant");

        var good = await TokenAsync(clientId, code, pkce.Verifier);
        good.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Token_RefusesACodeUsedTwice_AndEndsWhatTheFirstUseProduced()
    {
        var (_, browser) = await SignedInAsync();
        var clientId = await RegisterAsync();
        var pkce = Pkce.New();
        var code = await ApproveAsync(browser, await AuthorizeAsync(clientId, pkce, "profile:read"), new[] { "profile:read" });

        var first = await ExchangeAsync(clientId, code, pkce.Verifier);
        var access = first.GetProperty("access_token").GetString()!;
        (await IntrospectAsync(access)).GetProperty("active").GetBoolean().Should().BeTrue();

        var replay = await TokenAsync(clientId, code, pkce.Verifier);
        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await IntrospectAsync(access)).GetProperty("active").GetBoolean().Should().BeFalse(
            "a replayed code means it leaked, so everything it produced is revoked");
    }

    [Fact]
    public async Task Token_RefusesACodeForAnotherClientOrRedirect()
    {
        var (_, browser) = await SignedInAsync();
        var clientId = await RegisterAsync();
        var otherClient = await RegisterAsync();
        var pkce = Pkce.New();
        var code = await ApproveAsync(browser, await AuthorizeAsync(clientId, pkce, "profile:read"), new[] { "profile:read" });

        (await TokenAsync(otherClient, code, pkce.Verifier)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await TokenAsync(clientId, code, pkce.Verifier, redirect: "http://localhost:8123/elsewhere"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Authorize_WithAnUnregisteredRedirect_ShowsAnErrorInsteadOfRedirecting()
    {
        var clientId = await RegisterAsync();
        using var http = NoRedirectClient();

        var response = await http.GetAsync(AuthorizeUrl(clientId, Pkce.New(), "profile:read", redirect: "https://evil.example/steal"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Headers.Location.Should().BeNull("an unverified redirect must never receive anything");
    }

    [Fact]
    public async Task Authorize_WithoutPkce_RedirectsBackWithAnError()
    {
        var clientId = await RegisterAsync();
        using var http = NoRedirectClient();
        var url = $"/api/oauth/authorize?client_id={clientId}&redirect_uri={Uri.EscapeDataString(Redirect)}&response_type=code&state=xyz";

        var response = await http.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.Should().StartWith(Redirect);
        var query = HttpUtility.ParseQueryString(new Uri(location).Query);
        query["error"].Should().Be("invalid_request");
        query["state"].Should().Be("xyz");
    }

    [Fact]
    public async Task Authorize_RefusesPlainPkceAndUnknownResources()
    {
        var clientId = await RegisterAsync();
        using var http = NoRedirectClient();
        var pkce = Pkce.New();

        var plain = await http.GetAsync(AuthorizeUrl(clientId, pkce, "profile:read") + "&code_challenge_method=plain");
        // The duplicate parameter makes this ambiguous, so ask cleanly instead.
        var cleanPlain = await http.GetAsync(AuthorizeUrl(clientId, pkce, "profile:read", method: "plain"));
        HttpUtility.ParseQueryString(new Uri(cleanPlain.Headers.Location!.ToString()).Query)["error"].Should().Be("invalid_request");

        var badResource = await http.GetAsync(AuthorizeUrl(clientId, pkce, "profile:read", resource: "https://other.example/mcp"));
        HttpUtility.ParseQueryString(new Uri(badResource.Headers.Location!.ToString()).Query)["error"].Should().Be("invalid_target");
        plain.Should().NotBeNull();
    }

    [Fact]
    public async Task Consent_RequiresASignedInUser_AndExpiredOrUnknownRequestsAre404()
    {
        using var anonymous = _fixture.CreateClient();
        (await anonymous.PostAsJsonAsync("/api/oauth/authorization-requests/view", new { request = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var (_, browser) = await SignedInAsync();
        (await browser.PostAsJsonAsync("/api/oauth/authorization-requests/view", new { request = "does-not-exist" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deny_RedirectsWithAccessDenied_AndTheRequestCannotBeReused()
    {
        var (_, browser) = await SignedInAsync();
        var clientId = await RegisterAsync();
        var secret = await AuthorizeAsync(clientId, Pkce.New(), "profile:read");

        var denied = await browser.PostAsJsonAsync("/api/oauth/authorization-requests/decision", new { request = secret, approve = false });
        var url = (await denied.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("redirectUrl").GetString()!;
        HttpUtility.ParseQueryString(new Uri(url).Query)["error"].Should().Be("access_denied");

        (await browser.PostAsJsonAsync("/api/oauth/authorization-requests/decision",
            new { request = secret, approve = true, scopes = new[] { "profile:read" } }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Approve_WithNoScopes_IsRefused()
    {
        var (_, browser) = await SignedInAsync();
        var secret = await AuthorizeAsync(await RegisterAsync(), Pkce.New(), "profile:read");

        (await browser.PostAsJsonAsync("/api/oauth/authorization-requests/decision",
            new { request = secret, approve = true, scopes = Array.Empty<string>() }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Approve_CannotGrantMoreThanTheClientAskedFor()
    {
        var (_, browser) = await SignedInAsync();
        var clientId = await RegisterAsync();
        var pkce = Pkce.New();
        var secret = await AuthorizeAsync(clientId, pkce, "profile:read");
        var code = await ApproveAsync(browser, secret, new[] { "profile:read", "body:write", "nutrition:write" });
        var tokens = await ExchangeAsync(clientId, code, pkce.Verifier);

        (await IntrospectAsync(tokens.GetProperty("access_token").GetString()!)).GetProperty("scopes")
            .EnumerateArray().Select(e => e.GetString()).Should().BeEquivalentTo(new[] { "profile:read" });
    }

    // ---- refresh, revoke, audience ----

    [Fact]
    public async Task Refresh_Rotates_AndReuseOfAnOldRefreshTokenRevokesTheFamily()
    {
        var (_, browser) = await SignedInAsync();
        var clientId = await RegisterAsync();
        var pkce = Pkce.New();
        var code = await ApproveAsync(browser, await AuthorizeAsync(clientId, pkce, "profile:read"), new[] { "profile:read" });
        var first = await ExchangeAsync(clientId, code, pkce.Verifier);
        var firstRefresh = first.GetProperty("refresh_token").GetString()!;

        var second = await RefreshAsync(clientId, firstRefresh);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondTokens = await second.Content.ReadFromJsonAsync<JsonElement>();
        secondTokens.GetProperty("refresh_token").GetString().Should().NotBe(firstRefresh);
        (await IntrospectAsync(secondTokens.GetProperty("access_token").GetString()!)).GetProperty("active").GetBoolean().Should().BeTrue();

        var replay = await RefreshAsync(clientId, firstRefresh);
        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await IntrospectAsync(secondTokens.GetProperty("access_token").GetString()!)).GetProperty("active").GetBoolean()
            .Should().BeFalse("reusing a spent refresh token means it leaked, so the family ends");
        (await RefreshAsync(clientId, secondTokens.GetProperty("refresh_token").GetString()!))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Refresh_IsBoundToTheClientThatReceivedIt()
    {
        var (_, browser) = await SignedInAsync();
        var clientId = await RegisterAsync();
        var pkce = Pkce.New();
        var code = await ApproveAsync(browser, await AuthorizeAsync(clientId, pkce, "profile:read"), new[] { "profile:read" });
        var tokens = await ExchangeAsync(clientId, code, pkce.Verifier);

        (await RefreshAsync(await RegisterAsync(), tokens.GetProperty("refresh_token").GetString()!))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Revoke_EndsTheAccessAndRefreshTokens()
    {
        var (_, browser) = await SignedInAsync();
        var clientId = await RegisterAsync();
        var pkce = Pkce.New();
        var code = await ApproveAsync(browser, await AuthorizeAsync(clientId, pkce, "profile:read"), new[] { "profile:read" });
        var tokens = await ExchangeAsync(clientId, code, pkce.Verifier);
        var access = tokens.GetProperty("access_token").GetString()!;

        using var http = _fixture.CreateClient();
        var revoke = await http.PostAsync("/api/oauth/revoke", Form(("token", tokens.GetProperty("refresh_token").GetString()!), ("client_id", clientId)));
        revoke.StatusCode.Should().Be(HttpStatusCode.OK);

        (await IntrospectAsync(access)).GetProperty("active").GetBoolean().Should().BeFalse();
        (await RefreshAsync(clientId, tokens.GetProperty("refresh_token").GetString()!)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // An unknown token is not an error: the endpoint tells a caller nothing.
        (await http.PostAsync("/api/oauth/revoke", Form(("token", "mza_nope")))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Introspect_IsServiceOnly_AndChecksTheAudience()
    {
        var (_, browser) = await SignedInAsync();
        var clientId = await RegisterAsync();
        var pkce = Pkce.New();
        var code = await ApproveAsync(browser, await AuthorizeAsync(clientId, pkce, "profile:read"), new[] { "profile:read" });
        var access = (await ExchangeAsync(clientId, code, pkce.Verifier)).GetProperty("access_token").GetString()!;

        using var anonymous = _fixture.CreateClient();
        (await anonymous.PostAsJsonAsync("/api/oauth/introspect", new { token = access, audience = "mcp" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await browser.PostAsJsonAsync("/api/oauth/introspect", new { token = access, audience = "mcp" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized, "a signed-in browser is not the MCP service");

        (await IntrospectAsync(access, audience: "api")).GetProperty("active").GetBoolean().Should().BeFalse(
            "an MCP token is not valid for the API");
    }

    [Fact]
    public async Task ARevokedGrant_DeactivatesItsTokensAtOnce()
    {
        var (user, browser) = await SignedInAsync();
        var clientId = await RegisterAsync();
        var pkce = Pkce.New();
        var code = await ApproveAsync(browser, await AuthorizeAsync(clientId, pkce, "profile:read"), new[] { "profile:read" });
        var access = (await ExchangeAsync(clientId, code, pkce.Verifier)).GetProperty("access_token").GetString()!;

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
            await db.OAuthGrants.Where(g => g.UserId == user).ExecuteUpdateAsync(s => s.SetProperty(g => g.RevokedAt, DateTime.UtcNow));
        }

        (await IntrospectAsync(access)).GetProperty("active").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ABannedUser_CannotUseAnExistingToken()
    {
        var (user, browser) = await SignedInAsync();
        var clientId = await RegisterAsync();
        var pkce = Pkce.New();
        var code = await ApproveAsync(browser, await AuthorizeAsync(clientId, pkce, "profile:read"), new[] { "profile:read" });
        var access = (await ExchangeAsync(clientId, code, pkce.Verifier)).GetProperty("access_token").GetString()!;

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
            await db.Users.Where(u => u.Id == user).ExecuteUpdateAsync(s => s.SetProperty(u => u.Banned, true));
        }
        // The status service caches briefly, so clear it the way a ban does in production.
        using (var scope = _fixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserCacheInvalidator>().InvalidateAsync(user);
        }

        (await IntrospectAsync(access)).GetProperty("active").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ReconsentingReplacesTheGrantInsteadOfAddingAnother()
    {
        var (user, browser) = await SignedInAsync();
        var clientId = await RegisterAsync();

        for (var i = 0; i < 2; i++)
        {
            var pkce = Pkce.New();
            await ApproveAsync(browser, await AuthorizeAsync(clientId, pkce, "nutrition:write profile:read"),
                i == 0 ? new[] { "nutrition:write", "profile:read" } : new[] { "profile:read" });
        }

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var grants = await db.OAuthGrants.Where(g => g.UserId == user && g.RevokedAt == null).ToListAsync();
        grants.Should().ContainSingle().Which.Scopes.Should().BeEquivalentTo(new[] { "profile:read" });
    }

    // ---- registration ----

    [Theory]
    [InlineData("http://example.com/callback")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://example.com/cb#frag")]
    [InlineData("not a uri")]
    public async Task Register_RejectsUnsafeRedirectUris(string redirect)
    {
        using var http = _fixture.CreateClient();
        var response = await http.PostAsJsonAsync("/api/oauth/register", new { client_name = "x", redirect_uris = new[] { redirect } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().Should().Be("invalid_redirect_uri");
    }

    [Fact]
    public async Task Register_RefusesConfidentialClients()
    {
        using var http = _fixture.CreateClient();
        var response = await http.PostAsJsonAsync("/api/oauth/register", new
        {
            client_name = "x", redirect_uris = new[] { Redirect }, token_endpoint_auth_method = "client_secret_basic",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().Should().Be("invalid_client_metadata");
    }

    [Fact]
    public async Task Register_AcceptsLoopbackAnyPortAndPrivateUseSchemes()
    {
        using var http = _fixture.CreateClient();
        var response = await http.PostAsJsonAsync("/api/oauth/register",
            new { client_name = "Desktop", redirect_uris = new[] { "http://127.0.0.1:5555/cb", "cursor://anysphere.cursor/oauth" } });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();

        // A loopback client picks its port at run time.
        var clientId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("client_id").GetString()!;
        using var noRedirect = NoRedirectClient();
        var authorize = await noRedirect.GetAsync(AuthorizeUrl(clientId, Pkce.New(), "profile:read", redirect: "http://127.0.0.1:61234/cb"));
        authorize.StatusCode.Should().Be(HttpStatusCode.Redirect);
        authorize.Headers.Location!.ToString().Should().StartWith("http://localhost/oauth/consent?request=");
    }

    // ---- metadata and first-party clients ----

    [Fact]
    public async Task MetadataClient_IsFetchedOnce_AndShownAsVerifiedByItsHost()
    {
        var (_, browser) = await SignedInAsync();
        const string clientId = "https://app.example.com/oauth/client.json";
        _fixture.OAuthMetadata.Serve(new OAuthClientMetadata(clientId, "Example Agent", null, null, new[] { Redirect }));
        var before = _fixture.OAuthMetadata.Fetches;

        var secret = await AuthorizeAsync(clientId, Pkce.New(), "profile:read");
        await AuthorizeAsync(clientId, Pkce.New(), "profile:read");

        _fixture.OAuthMetadata.Fetches.Should().Be(before + 1, "the document is cached for a day");
        var view = await (await browser.PostAsJsonAsync("/api/oauth/authorization-requests/view", new { request = secret }))
            .Content.ReadFromJsonAsync<JsonElement>();
        view.GetProperty("source").GetString().Should().Be("metadata");
        view.GetProperty("verifiedHost").GetString().Should().Be("app.example.com");
        view.GetProperty("clientName").GetString().Should().Be("Example Agent");
    }

    [Fact]
    public async Task MetadataClient_WhoseDocumentCannotBeLoaded_IsUnknown()
    {
        using var http = NoRedirectClient();
        var response = await http.GetAsync(AuthorizeUrl("https://missing.example.com/client.json", Pkce.New(), "profile:read"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task FirstPartyClient_MayAskForTheApiAudienceAndFullScope_ButOthersMayNot()
    {
        var (_, browser) = await SignedInAsync();
        var pkce = Pkce.New();
        const string appRedirect = "com.zaftech.mizan.test:/oauth2redirect";

        var secret = await AuthorizeAsync("test-first-party", pkce, "full", redirect: appRedirect, resource: "http://localhost/api");
        var code = await ApproveAsync(browser, secret, new[] { "full" });
        var tokens = await ExchangeAsync("test-first-party", code, pkce.Verifier, redirect: appRedirect);

        tokens.GetProperty("scope").GetString().Should().Be("full");
        (await IntrospectAsync(tokens.GetProperty("access_token").GetString()!, audience: "api")).GetProperty("active").GetBoolean().Should().BeTrue();

        // A dynamically registered client may not ask for the API audience.
        var clientId = await RegisterAsync();
        using var http = NoRedirectClient();
        var denied = await http.GetAsync(AuthorizeUrl(clientId, Pkce.New(), "profile:read", resource: "http://localhost/api"));
        HttpUtility.ParseQueryString(new Uri(denied.Headers.Location!.ToString()).Query)["error"].Should().Be("invalid_target");
    }

    // ---- audit log hygiene ----

    [Fact]
    public async Task TheAuditLog_NeverStoresAPasswordOrAnOAuthSecret()
    {
        await _fixture.ResetDatabaseAsync();
        const string password = "correct-horse-battery-staple";
        var email = $"audit-{Guid.NewGuid():N}@example.com";
        using var client = _fixture.CreateClient();

        await client.PostAsJsonAsync("/api/Auth/register", new { Email = email, Password = password, Name = "Audit" });
        await _fixture.DrainOutboxAsync();
        var verifyToken = _fixture.Email.LastTokenFor(email, "verifyemail")!;
        await client.PostAsJsonAsync("/api/Auth/verify-email", new { Token = verifyToken });
        var login = await client.PostAsJsonAsync("/api/Auth/login", new { Email = email, Password = password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var clientId = await RegisterAsync();
        var pkce = Pkce.New();
        var secret = await AuthorizeAsync(clientId, pkce, "profile:read");
        var cookie = login.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        using var browser = _fixture.CreateClient();
        browser.DefaultRequestHeaders.Add("Cookie", cookie);
        await ApproveAsync(browser, secret, new[] { "profile:read" });

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var details = await db.AuditLogs.Select(a => a.Details ?? "").ToListAsync();

        details.Should().NotBeEmpty();
        details.Should().OnlyContain(d => !d.Contains(password) && !d.Contains(verifyToken) && !d.Contains(secret));
        (await db.AuditLogs.Select(a => a.Action).ToListAsync()).Should().Contain("LoginCommand").And.Contain("DecideAuthorizationCommand");
    }

    // ---- helpers ----

    private async Task<(Guid UserId, HttpClient Browser)> SignedInAsync(string role = "user")
    {
        await _fixture.ResetDatabaseAsync();
        var id = Guid.NewGuid();
        var email = $"oauth-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email, role: role);
        return (id, _fixture.CreateAuthenticatedClient(id, email, role));
    }

    private async Task<Guid> SeedOtherUserAsync()
    {
        var id = Guid.NewGuid();
        await _fixture.SeedUserAsync(id, $"other-{id:N}@example.com");
        return id;
    }

    private async Task<Guid> SeedHouseholdAsync(Guid userId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var household = new Household { Id = Guid.NewGuid(), Name = "Home", CreatedBy = userId, CreatedAt = DateTime.UtcNow };
        db.Households.Add(household);
        db.HouseholdMembers.Add(new HouseholdMember
        {
            HouseholdId = household.Id, UserId = userId, Role = "owner", JoinedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return household.Id;
    }

    private async Task<string> RegisterAsync()
    {
        using var http = _fixture.CreateClient();
        var response = await http.PostAsJsonAsync("/api/oauth/register", new { client_name = "Test Assistant", redirect_uris = new[] { Redirect } });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("client_id").GetString()!;
    }

    private HttpClient NoRedirectClient() =>
        _fixture.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static string AuthorizeUrl(string clientId, Pkce pkce, string scope, string redirect = Redirect,
        string method = "S256", string? resource = null) =>
        $"/api/oauth/authorize?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirect)}"
        + $"&response_type=code&scope={Uri.EscapeDataString(scope)}&state=s1&code_challenge={pkce.Challenge}&code_challenge_method={method}"
        + (resource is null ? "" : $"&resource={Uri.EscapeDataString(resource)}");

    /// <summary>Starts the flow and returns the secret the consent page would receive.</summary>
    private async Task<string> AuthorizeAsync(string clientId, Pkce pkce, string scope, string redirect = Redirect, string? resource = null)
    {
        using var http = NoRedirectClient();
        var response = await http.GetAsync(AuthorizeUrl(clientId, pkce, scope, redirect, resource: resource));
        response.StatusCode.Should().Be(HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
        var location = response.Headers.Location!.ToString();
        location.Should().StartWith("http://localhost/oauth/consent?request=");
        return HttpUtility.ParseQueryString(new Uri(location).Query)["request"]!;
    }

    private static async Task<string> ApproveAsync(HttpClient browser, string secret, string[] scopes,
        string householdMode = "none", Guid[]? households = null)
    {
        var response = await browser.PostAsJsonAsync("/api/oauth/authorization-requests/decision", new
        {
            request = secret, approve = true, scopes, householdMode, householdIds = households ?? Array.Empty<Guid>(),
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var url = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("redirectUrl").GetString()!;
        var query = HttpUtility.ParseQueryString(new Uri(url).Query);
        query["state"].Should().Be("s1");
        query["iss"].Should().Be("http://localhost/api");
        return query["code"]!;
    }

    private async Task<HttpResponseMessage> TokenAsync(string clientId, string code, string verifier, string redirect = Redirect)
    {
        using var http = _fixture.CreateClient();
        return await http.PostAsync("/api/oauth/token", Form(
            ("grant_type", "authorization_code"), ("client_id", clientId), ("code", code),
            ("code_verifier", verifier), ("redirect_uri", redirect)));
    }

    private async Task<JsonElement> ExchangeAsync(string clientId, string code, string verifier, string redirect = Redirect)
    {
        var response = await TokenAsync(clientId, code, verifier, redirect);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<HttpResponseMessage> RefreshAsync(string clientId, string refresh)
    {
        using var http = _fixture.CreateClient();
        return await http.PostAsync("/api/oauth/token", Form(("grant_type", "refresh_token"), ("client_id", clientId), ("refresh_token", refresh)));
    }

    private async Task<JsonElement> IntrospectAsync(string token, string audience = "mcp")
    {
        using var http = _fixture.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
        var response = await http.PostAsJsonAsync("/api/oauth/introspect", new { token, audience });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields) =>
        new(fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));

    private sealed record Pkce(string Verifier, string Challenge)
    {
        public static Pkce New()
        {
            var verifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return new Pkce(verifier, challenge);
        }
    }
}
