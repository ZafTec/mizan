using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// How the Android app reaches the API: the access token from the authorization server,
/// sent as a bearer token. It must open the same endpoints as the session cookie does,
/// and nothing else may use it as a way in.
/// </summary>
[Collection("ApiIntegration")]
public class NativeAccessTests
{
    private const string Me = "/api/Users/me";
    private readonly ApiTestFixture _fixture;

    public NativeAccessTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AnApiToken_OpensTheSameEndpointsAsTheSessionCookie()
    {
        var (user, access) = await AppAsync();

        using var client = Bearer(access.Token);
        var response = await client.GetAsync(Me);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain(user.ToString());
    }

    [Fact]
    public async Task NoToken_AndABadToken_AreTurnedAway()
    {
        await AppAsync();

        using var anonymous = _fixture.CreateClient();
        (await anonymous.GetAsync(Me)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var wrong = Bearer("mza_not-a-real-token");
        (await wrong.GetAsync(Me)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ATokenIssuedForTheMcpServer_IsNotAcceptedByTheApi()
    {
        var user = await UserAsync();
        var mcp = await _fixture.CreateMcpAccessAsync(user);

        using var client = Bearer(mcp.Token);

        (await client.GetAsync(Me)).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "an app granted access to the assistant tools must not be able to call the API as the user");
    }

    [Fact]
    public async Task RevokingTheConnection_ClosesTheDoorAtOnce()
    {
        var (user, access) = await AppAsync();
        using var client = Bearer(access.Token);
        (await client.GetAsync(Me)).StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
            await db.OAuthGrants.Where(g => g.Id == access.GrantId).ExecuteUpdateAsync(s => s.SetProperty(g => g.RevokedAt, DateTime.UtcNow));
        }

        (await client.GetAsync(Me)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnExpiredToken_IsRefused()
    {
        var (_, access) = await AppAsync();
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
            await db.OAuthTokens.Where(t => t.GrantId == access.GrantId).ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        }

        using var client = Bearer(access.Token);
        (await client.GetAsync(Me)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TheLiveChannel_TakesTheTokenFromTheQuery_ButNoOtherPathDoes()
    {
        var (_, access) = await AppAsync();
        using var client = _fixture.CreateClient();

        (await client.PostAsync("/hubs/chat/negotiate?negotiateVersion=1", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync($"/hubs/chat/negotiate?negotiateVersion=1&access_token={access.Token}", null)).StatusCode
            .Should().Be(HttpStatusCode.OK, "a WebSocket cannot send a header");
        (await client.GetAsync($"{Me}?access_token={access.Token}")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized, "a token in a URL is logged, so it is accepted only where it has to be");
    }

    // ---- helpers ----

    private async Task<Guid> UserAsync()
    {
        await _fixture.ResetDatabaseAsync();
        var user = Guid.NewGuid();
        await _fixture.SeedUserAsync(user, $"native-{user:N}@example.com");
        return user;
    }

    private async Task<(Guid User, ApiTestFixture.McpAccess Access)> AppAsync()
    {
        var user = await UserAsync();
        return (user, await _fixture.CreateMcpAccessAsync(user, [Mizan.Contracts.Mcp.McpScopes.Full], audience: "api"));
    }

    private HttpClient Bearer(string token)
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
