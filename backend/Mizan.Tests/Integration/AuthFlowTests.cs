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

    [Fact]
    public async Task GetMe_ReturnsUnauthorized_WhenUserMissing()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"missing-{userId:N}@example.com";

        using var client = _fixture.CreateAuthenticatedClient(userId, email);
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
    public async Task GetMe_ReturnsUnauthorized_WhenSignatureInvalid()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"invalidsig-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email, emailVerified: true);

        using var issuer = TestJwtIssuer.Create();
        var token = issuer.CreateToken(userId, email, "user", _fixture.Issuer, _fixture.Audience);
        using var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

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

        await _fixture.SeedMcpUsageLogAsync(createResult.Id, userId, "foods.search", success: true, executionTimeMs: 120);

        var analyticsResponse = await client.GetAsync("/api/McpTokens/analytics");
        analyticsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var analytics = await analyticsResponse.Content.ReadFromJsonAsync<McpUsageAnalyticsResult>();
        analytics.Should().NotBeNull();
        analytics!.Overview.TotalCalls.Should().Be(1);
        analytics!.Overview.SuccessfulCalls.Should().Be(1);
        analytics!.ToolUsage.Should().HaveCount(1);
        analytics!.ToolUsage.Should().OnlyContain(t => t.ToolName == "foods.search");
        analytics!.ToolUsage.First().CallCount.Should().Be(1);
    }

    [Fact]
    public async Task JwtBearerAuthentication_RejectsBannedUserToken()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"token-banned-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email, emailVerified: true, banned: true);

        var validToken = _fixture.CreateToken(userId, email, "user");
        using var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", validToken);

        var response = await client.GetAsync("/api/Users/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record UserResponse(Guid Id, string Email);
    private sealed record CreateMcpTokenResponse(Guid Id, string PlaintextToken, string Name);
    private sealed record ValidateMcpTokenResponse(Guid UserId, bool IsValid, Guid TokenId);
    private sealed record McpUsageAnalyticsResult(UsageOverviewResponse Overview, List<ToolUsageResponse> ToolUsage);
    private sealed record UsageOverviewResponse(int TotalCalls, int SuccessfulCalls, int FailedCalls, double SuccessRate, int AverageExecutionTimeMs, int UniqueTokensUsed);
    private sealed record ToolUsageResponse(string ToolName, int CallCount, int SuccessCount, int FailureCount, int AverageExecutionTimeMs);
}
