using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// An administrator can view the site as a user for an hour. They cannot do lasting damage from there, every
/// audited action names them, and leaving puts their own session back.
/// </summary>
[Collection("ApiIntegration")]
public class ImpersonationTests
{
    private readonly ApiTestFixture _fixture;

    public ImpersonationTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AnAdmin_ViewsTheSiteAsTheUser_AndTheUserIsWhoTheServerSees()
    {
        var w = await WorldAsync();
        var browser = w.AdminBrowser();

        var start = await browser.PostAsync($"/api/admin/users/{w.User}/impersonate");

        start.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await (await browser.GetAsync("/api/Auth/me")).Content.ReadFromJsonAsync<JsonElement>();
        me.GetProperty("id").GetGuid().Should().Be(w.User);
        me.GetProperty("role").GetString().Should().Be("user", "the administrator does not bring their powers along");
        me.GetProperty("impersonation").GetProperty("impersonatorId").GetGuid().Should().Be(w.Admin);
        me.GetProperty("impersonation").GetProperty("expiresAt").GetDateTime().Should().BeCloseTo(DateTime.UtcNow.AddHours(1), TimeSpan.FromMinutes(2));
        browser.Has("mizan_admin_session").Should().BeTrue("the administrator's own session waits aside");
    }

    [Fact]
    public async Task WhileViewing_TheAdminAreaIsClosed()
    {
        var w = await WorldAsync();
        var browser = w.AdminBrowser();
        await browser.PostAsync($"/api/admin/users/{w.User}/impersonate");

        (await browser.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OrdinaryUseWorks_AndIsAuditedWithTheAdminsName()
    {
        var w = await WorldAsync();
        var browser = w.AdminBrowser();
        await browser.PostAsync($"/api/admin/users/{w.User}/impersonate");

        var logged = await browser.PostJsonAsync("/api/BodyMeasurements", new { Date = DateTime.UtcNow, WeightKg = 80m });

        logged.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var entry = await db.AuditLogs.Where(a => a.Action == "LogBodyMeasurementCommand" || a.Action.StartsWith("LogMeasurement")).OrderByDescending(a => a.Timestamp).FirstAsync();
        entry.UserId.Should().Be(w.User);
        entry.ImpersonatorId.Should().Be(w.Admin);
    }

    [Fact]
    public async Task StartingIsAudited_WithTheTargetAndTheAdmin()
    {
        var w = await WorldAsync();
        var browser = w.AdminBrowser();

        await browser.PostAsync($"/api/admin/users/{w.User}/impersonate");

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var entry = await db.AuditLogs.SingleAsync(a => a.Action == "StartImpersonationCommand");
        entry.UserId.Should().Be(w.Admin);
        entry.EntityId.Should().Be(w.User.ToString());
    }

    [Theory]
    [InlineData("POST", "/api/Auth/change-password")]
    [InlineData("DELETE", "/api/Auth/account")]
    [InlineData("DELETE", "/api/Auth/sessions/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/Subscriptions/cancel")]
    [InlineData("POST", "/api/Subscriptions/portal")]
    [InlineData("POST", "/api/Telegram/link")]
    [InlineData("DELETE", "/api/Telegram/link")]
    [InlineData("POST", "/api/oauth/decide")]
    [InlineData("DELETE", "/api/McpConnections/00000000-0000-0000-0000-000000000001")]
    [InlineData("GET", "/api/Auth/external/google")]
    public async Task LastingChanges_AreRefusedWhileViewing(string method, string path)
    {
        var w = await WorldAsync();
        var browser = w.AdminBrowser();
        await browser.PostAsync($"/api/admin/users/{w.User}/impersonate");

        var response = await browser.SendAsync(new HttpMethod(method), path);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().Should().Be("impersonation_restricted");
    }

    [Fact]
    public async Task TheSameRequests_AreFineForTheUserThemselves()
    {
        var w = await WorldAsync();
        var browser = w.UserBrowser();

        // Not refused by the guard (it may fail for its own reasons, but never as impersonation_restricted).
        var response = await browser.SendAsync(HttpMethod.Delete, "/api/Telegram/link");

        (await response.Content.ReadAsStringAsync()).Should().NotContain("impersonation_restricted");
    }

    [Fact]
    public async Task LeavingPutsTheAdminsOwnSessionBack_AndEndsTheViewForGood()
    {
        var w = await WorldAsync();
        var browser = w.AdminBrowser();
        await browser.PostAsync($"/api/admin/users/{w.User}/impersonate");
        var viewingToken = browser.Value("mizan_session");

        var stop = await browser.PostAsync("/api/Auth/impersonation/stop");

        stop.StatusCode.Should().Be(HttpStatusCode.OK);
        (await stop.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("restored").GetBoolean().Should().BeTrue();
        var me = await (await browser.GetAsync("/api/Auth/me")).Content.ReadFromJsonAsync<JsonElement>();
        me.GetProperty("id").GetGuid().Should().Be(w.Admin);
        me.TryGetProperty("impersonation", out var imp).Should().BeTrue();
        imp.ValueKind.Should().Be(JsonValueKind.Null);
        browser.Has("mizan_admin_session").Should().BeFalse();

        // The view's token is dead, not merely forgotten by the browser.
        var replay = new Browser(_fixture, viewingToken);
        (await replay.GetAsync("/api/Auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task IfTheAdminsOwnSessionHasExpired_LeavingSignsThemOut()
    {
        var w = await WorldAsync();
        var browser = w.AdminBrowser();
        await browser.PostAsync($"/api/admin/users/{w.User}/impersonate");
        using (var scope = _fixture.Services.CreateScope())
        {
            // Revoking through the service also clears its short lookup cache, as signing out elsewhere would.
            await scope.ServiceProvider.GetRequiredService<Mizan.Application.Interfaces.ISessionService>().RevokeAsync(w.AdminToken);
        }

        var stop = await browser.PostAsync("/api/Auth/impersonation/stop");

        (await stop.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("restored").GetBoolean().Should().BeFalse();
        (await browser.GetAsync("/api/Auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnotherUsersAdminCookie_CannotBeSmuggledIn()
    {
        var w = await WorldAsync();
        var otherAdmin = Guid.NewGuid();
        await _fixture.SeedUserAsync(otherAdmin, $"admin2-{otherAdmin:N}@example.com", role: "admin");
        var otherToken = await _fixture.CreateSessionAsync(otherAdmin);
        var browser = w.AdminBrowser();
        await browser.PostAsync($"/api/admin/users/{w.User}/impersonate");
        browser.Set("mizan_admin_session", otherToken);

        var stop = await browser.PostAsync("/api/Auth/impersonation/stop");

        (await stop.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("restored").GetBoolean().Should().BeFalse(
            "the saved session must belong to the administrator who opened the view");
    }

    [Fact]
    public async Task TheViewEndsAnHourAfterItStarts()
    {
        var w = await WorldAsync();
        var browser = w.AdminBrowser();
        await browser.PostAsync($"/api/admin/users/{w.User}/impersonate");
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
            await db.UserSessions.Where(s => s.ImpersonatorId != null).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        }

        // The new token has not been used yet, so no cached lookup can still say it is good.
        (await browser.GetAsync("/api/Auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("self")]
    [InlineData("banned")]
    [InlineData("unverified")]
    [InlineData("missing")]
    public async Task SomeAccounts_CannotBeViewed(string kind)
    {
        var w = await WorldAsync();
        var target = kind switch
        {
            "admin" => await SeedAsync("admin", role: "admin"),
            "self" => w.Admin,
            "banned" => await SeedAsync("banned", banned: true),
            "unverified" => await SeedAsync("unverified", verified: false),
            _ => Guid.NewGuid(),
        };
        var browser = w.AdminBrowser();

        var response = await browser.PostAsync($"/api/admin/users/{target}/impersonate");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        browser.Has("mizan_admin_session").Should().BeFalse("nothing changed");
    }

    [Fact]
    public async Task OnlyAnAdmin_MayStart()
    {
        var w = await WorldAsync();
        var browser = w.UserBrowser();

        (await browser.PostAsync($"/api/admin/users/{w.User}/impersonate")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ViewsCannotBeNested()
    {
        var w = await WorldAsync();
        var second = await SeedAsync("second");
        var browser = w.AdminBrowser();
        await browser.PostAsync($"/api/admin/users/{w.User}/impersonate");

        var again = await browser.PostAsync($"/api/admin/users/{second}/impersonate");

        again.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- helpers ----

    private async Task<Guid> SeedAsync(string name, string role = "user", bool banned = false, bool verified = true)
    {
        var id = Guid.NewGuid();
        await _fixture.SeedUserAsync(id, $"{name}-{id:N}@example.com", emailVerified: verified, role: role, banned: banned);
        return id;
    }

    private async Task<World> WorldAsync()
    {
        await _fixture.ResetDatabaseAsync();
        var admin = await SeedAsync("admin", role: "admin");
        var user = await SeedAsync("user");
        return new World(_fixture, admin, user, await _fixture.CreateSessionAsync(admin), await _fixture.CreateSessionAsync(user));
    }

    private sealed record World(ApiTestFixture Fixture, Guid Admin, Guid User, string AdminToken, string UserToken)
    {
        public Browser AdminBrowser() => new(Fixture, AdminToken);
        public Browser UserBrowser() => new(Fixture, UserToken);
    }

    /// <summary>A browser's cookie jar, which the test server does not keep for us.</summary>
    private sealed class Browser
    {
        private readonly ApiTestFixture _fixture;
        private readonly Dictionary<string, string> _cookies = new();

        public Browser(ApiTestFixture fixture, string sessionToken)
        {
            _fixture = fixture;
            _cookies["mizan_session"] = sessionToken;
        }

        public bool Has(string name) => _cookies.ContainsKey(name);
        public string Value(string name) => _cookies[name];
        public void Set(string name, string value) => _cookies[name] = value;

        public Task<HttpResponseMessage> GetAsync(string path) => SendAsync(HttpMethod.Get, path);
        public Task<HttpResponseMessage> PostAsync(string path) => SendAsync(HttpMethod.Post, path);
        public Task<HttpResponseMessage> PostJsonAsync(string path, object body) => SendAsync(HttpMethod.Post, path, JsonContent.Create(body));

        public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content = null)
        {
            using var client = _fixture.CreateClient();
            using var request = new HttpRequestMessage(method, path) { Content = content };
            if (_cookies.Count > 0) request.Headers.Add("Cookie", string.Join("; ", _cookies.Select(c => $"{c.Key}={c.Value}")));
            var response = await client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            response.Content = new StringContent(text, System.Text.Encoding.UTF8, "application/json");

            if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                foreach (var header in setCookies)
                {
                    var first = header.Split(';')[0];
                    var eq = first.IndexOf('=');
                    var name = first[..eq];
                    var value = first[(eq + 1)..];
                    var expired = header.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase) || value.Length == 0;
                    if (expired) _cookies.Remove(name); else _cookies[name] = value;
                }
            }

            return response;
        }
    }
}
