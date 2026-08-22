using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public class AuthFlowTests
{
    private readonly ApiTestFixture _fixture;

    public AuthFlowTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetMe_ReturnsUser_WhenTokenValid()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"user-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email, emailVerified: true);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);
        var response = await client.GetAsync("/api/Users/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        user.Should().NotBeNull();
        user!.Id.Should().Be(userId);
        user.Email.Should().Be(email);
    }

    /// <summary>
    /// Replaces the old "session for a user that does not exist" case. Since
    /// user_sessions is a real foreign key onto users, that row cannot be
    /// written at all and deleting the user cascades the session away - the
    /// scenario is now unreachable rather than merely rejected. Expiry is the
    /// case that remains.
    /// </summary>
    [Fact]
    public async Task GetMe_ReturnsUnauthorized_WhenSessionExpired()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"expired-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email, emailVerified: true);

        var token = await _fixture.CreateSessionAsync(userId, DateTime.UtcNow.AddMinutes(-1));
        using var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{ApiTestFixture.SessionCookieName}={token}");

        var response = await client.GetAsync("/api/Users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_ReturnsUnauthorized_WhenEmailNotVerified()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"unverified-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email, emailVerified: false);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);
        var response = await client.GetAsync("/api/Users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_ReturnsUnauthorized_WhenUserBanned()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"banned-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email, emailVerified: true, banned: true);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);
        var response = await client.GetAsync("/api/Users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_ReturnsUnauthorized_WhenSessionTokenIsNotOurs()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"forged-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email, emailVerified: true);

        using var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add(
            "Cookie", $"{ApiTestFixture.SessionCookieName}=not-a-real-session-token");

        var response = await client.GetAsync("/api/Users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task McpTokens_ValidatesTokenAndUpdatesLastUsed()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"usage-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email, emailVerified: true);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var createResponse = await client.PostAsJsonAsync("/api/McpTokens", new { Name = "Usage Test Token" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateMcpTokenResponse>();
        var plaintextToken = createResult!.PlaintextToken;

        await Task.Delay(100);

        var validateResponse = await client.PostAsJsonAsync("/api/McpTokens/validate", new { Token = plaintextToken });
        validateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var validateResult = await validateResponse.Content.ReadFromJsonAsync<ValidateMcpTokenResponse>();
        validateResult.Should().NotBeNull();
        validateResult!.IsValid.Should().BeTrue();
        validateResult.TokenId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task McpTokens_UnauthenticatedUserCannotCreateToken()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"unauth-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email, emailVerified: true);

        using var client = _fixture.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/McpTokens", new { Name = "Test Token" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task McpTokens_AnalyticsReturnsUsageSummary()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"analytics-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email, emailVerified: true);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var createResponse = await client.PostAsJsonAsync("/api/McpTokens", new { Name = "Analytics Token" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateMcpTokenResponse>();
        var plaintextToken = createResult!.PlaintextToken;

        await _fixture.SeedMcpUsageLogAsync(createResult.Id, userId, "search_foods", success: true, executionTimeMs: 120);

        var analyticsResponse = await client.GetAsync("/api/McpTokens/analytics");
        analyticsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var analytics = await analyticsResponse.Content.ReadFromJsonAsync<McpUsageAnalyticsResult>();
        analytics.Should().NotBeNull();
        analytics!.Overview.TotalCalls.Should().Be(1);
        analytics!.Overview.SuccessfulCalls.Should().Be(1);
        analytics!.ToolUsage.Should().HaveCount(1);
        analytics!.ToolUsage.Should().OnlyContain(t => t.ToolName == "search_foods");
        analytics!.ToolUsage.First().CallCount.Should().Be(1);
    }

    [Fact]
    public async Task RegularMcpKey_CannotReachAdminEndpoint()
    {
        await _fixture.ResetDatabaseAsync();
        var adminId = Guid.NewGuid();
        await _fixture.SeedUserAsync(adminId, $"admin-{adminId:N}@example.com", role: "admin");
        using var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
        client.DefaultRequestHeaders.Add("X-Impersonate-User", adminId.ToString());

        var response = await client.GetAsync("/api/admin/social/analytics");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The thing a JWT could not do. Signing out one session has to take effect
    /// on the next request, not at the end of a token lifetime.
    /// </summary>
    [Fact]
    public async Task RevokedSession_StopsWorkingImmediately()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"revoked-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email, emailVerified: true);

        var token = await _fixture.CreateSessionAsync(userId);
        using var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{ApiTestFixture.SessionCookieName}={token}");

        (await client.GetAsync("/api/Users/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.PostAsync("/api/Auth/logout", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync("/api/Users/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record UserResponse(Guid Id, string Email);
    private sealed record CreateMcpTokenResponse(Guid Id, string PlaintextToken, string Name);
    private sealed record ValidateMcpTokenResponse(Guid UserId, bool IsValid, Guid TokenId);
    private sealed record McpUsageAnalyticsResult(UsageOverviewResponse Overview, List<ToolUsageResponse> ToolUsage);
    private sealed record UsageOverviewResponse(int TotalCalls, int SuccessfulCalls, int FailedCalls, double SuccessRate, int AverageExecutionTimeMs, int UniqueTokensUsed);
    private sealed record ToolUsageResponse(string ToolName, int CallCount, int SuccessCount, int FailureCount, int AverageExecutionTimeMs);
}
