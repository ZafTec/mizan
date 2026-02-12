using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Mizan.Application.Common;
using Mizan.Application.Queries;
using Xunit;

namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public class McpTokensControllerTests
{
    private readonly ApiTestFixture _fixture;

    public McpTokensControllerTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CanCreateValidateAndRevokeToken()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"mcp-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var createResponse = await client.PostAsJsonAsync("/api/McpTokens", new { Name = "cli" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created); // 201 Created

        var created = await createResponse.Content.ReadFromJsonAsync<CreateMcpTokenResponse>();
        created.Should().NotBeNull();
        created!.PlaintextToken.Should().StartWith("mcp_");
        created.PlaintextToken.Length.Should().Be(68);

        var listResponse = await client.GetAsync("/api/McpTokens");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await listResponse.Content.ReadFromJsonAsync<PagedResult<McpTokenDto>>();
        list.Should().NotBeNull();
        list!.Items.Should().Contain(t => t.Id == created.Id);

        var validateResponse = await client.PostAsJsonAsync("/api/McpTokens/validate", new { Token = created.PlaintextToken });
        validateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var validated = await validateResponse.Content.ReadFromJsonAsync<ValidateTokenResponse>();
        validated.Should().NotBeNull();
        validated!.IsValid.Should().BeTrue();
        validated.UserId.Should().Be(userId);

        var revokeResponse = await client.DeleteAsync($"/api/McpTokens/{created.Id}");
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var validateAfterRevoke = await client.PostAsJsonAsync("/api/McpTokens/validate", new { Token = created.PlaintextToken });
        validateAfterRevoke.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnalyticsReturnsUsageSummary()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"analytics-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var createResponse = await client.PostAsJsonAsync("/api/McpTokens", new { Name = "analytics" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created); // 201 Created

        var created = await createResponse.Content.ReadFromJsonAsync<CreateMcpTokenResponse>();
        created.Should().NotBeNull();

        await _fixture.SeedMcpUsageLogAsync(created!.Id, userId, "foods.search", success: true, executionTimeMs: 120);

        var analyticsResponse = await client.GetAsync("/api/McpTokens/analytics");
        analyticsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var analytics = await analyticsResponse.Content.ReadFromJsonAsync<McpUsageAnalyticsResponse>();
        analytics.Should().NotBeNull();
        analytics!.Overview.TotalCalls.Should().Be(1);
        analytics.Overview.SuccessfulCalls.Should().Be(1);
        analytics.ToolUsage.Should().Contain(t => t.ToolName == "foods.search");
    }

    private sealed record CreateMcpTokenResponse(Guid Id, string PlaintextToken, string Name);
    private sealed record ValidateTokenResponse(Guid UserId, bool IsValid, Guid? TokenId);
    private sealed record McpUsageAnalyticsResponse(UsageOverviewResponse Overview, List<ToolUsageResponse> ToolUsage);
    private sealed record UsageOverviewResponse(int TotalCalls, int SuccessfulCalls, int FailedCalls, double SuccessRate, int AverageExecutionTimeMs, int UniqueTokensUsed);
    private sealed record ToolUsageResponse(string ToolName, int CallCount, int SuccessCount, int FailureCount, int AverageExecutionTimeMs);
}
