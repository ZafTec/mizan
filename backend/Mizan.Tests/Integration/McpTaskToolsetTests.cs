extern alias McpServer;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using McpServer::Mizan.Mcp.Server.Services;
using Xunit;

namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public class McpTaskToolsetTests : IClassFixture<WebApplicationFactory<McpServer::Program>>, IAsyncLifetime
{
    private readonly HttpClient _mcpClient;
    private readonly ApiTestFixture _apiFixture;

    public McpTaskToolsetTests(WebApplicationFactory<McpServer::Program> mcpFactory, ApiTestFixture apiFixture)
    {
        _apiFixture = apiFixture;
        var factory = mcpFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("MizanApiUrl", "http://localhost:5000");
            builder.UseSetting("ServiceApiKey", "test-api-key");
            builder.UseSetting("Mcp:ServiceApiKey", "test-api-key");
            builder.UseSetting("Mcp:AdminServiceApiKey", "test-admin-api-key");
            builder.UseSetting("Mcp:Toolset", "tasks");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBackendApiClient>();
                services.AddScoped<IBackendApiClient>(sp =>
                {
                    var client = new HttpClient(_apiFixture.Server.CreateHandler()) { BaseAddress = new Uri("http://localhost:5000") };
                    client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
                    return new BackendApiClient(client, sp.GetRequiredService<IHttpContextAccessor>(),
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BackendApiClient>>());
                });
            });
        });
        _mcpClient = factory.CreateClient();
    }

    public Task InitializeAsync() => _apiFixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_connection_allowed_only_nutrition_sees_the_nutrition_task_tools_and_its_call_is_logged_and_counted()
    {
        var userId = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(userId, "mcp-tasks@example.com", emailVerified: true);
        var access = await _apiFixture.CreateMcpAccessAsync(userId, ["nutrition:read", "nutrition:write"]);
        _mcpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access.Token);

        var listed = await SendAsync("tools/list", new { });
        var tools = listed.GetProperty("tools").EnumerateArray().ToDictionary(t => t.GetProperty("name").GetString()!);
        tools.Keys.Should().Contain(["find_food", "get_nutrition", "log_food"]);
        tools.Keys.Should().NotContain(["get_training", "plan_meals", "get_body_trend", "search_foods"]);
        tools["get_nutrition"].GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean().Should().BeTrue();
        tools["get_nutrition"].GetProperty("_meta").GetProperty("ui").GetProperty("resourceUri").GetString()
            .Should().Be("ui://mizan/nutrition-day.html");

        using (var web = _apiFixture.CreateAuthenticatedClient(userId, "mcp-tasks@example.com"))
        {
            (await web.PostAsJsonAsync("/api/Goals", new { GoalType = "general", TargetCalories = 2100, TargetProteinGrams = 130, TargetCarbsGrams = 200, TargetFatGrams = 70 }))
                .IsSuccessStatusCode.Should().BeTrue();
        }

        var called = await SendAsync("tools/call", new { name = "get_nutrition", arguments = new { view = "day", date = "2026-11-07" } });
        (called.TryGetProperty("isError", out var isError) && isError.GetBoolean()).Should().BeFalse(called.ToString());

        // Issue #132: the nutrition widget draws its ring and bars from these fields.
        var day = called.GetProperty("structuredContent");
        foreach (var field in new[] { "date", "totalCalories", "totalProtein", "totalCarbs", "totalFat" })
            day.TryGetProperty(field, out _).Should().BeTrue(field);
        day.GetProperty("targetCalories").GetDecimal().Should().Be(2100);
        day.GetProperty("targetProtein").GetDecimal().Should().Be(130);
        day.GetProperty("targetCarbs").GetDecimal().Should().Be(200);
        day.GetProperty("targetFat").GetDecimal().Should().Be(70);

        var logs = await _apiFixture.GetMcpUsageLogsByUserId(userId);
        logs.Should().ContainSingle(l => l.ToolName == "get_nutrition" && l.Success && l.GrantId == access.GrantId);
    }

    private async Task<JsonElement> SendAsync(string method, object parameters)
    {
        var response = await _mcpClient.PostMcpAsync(new { jsonrpc = "2.0", id = Guid.NewGuid().ToString("N"), method, @params = parameters });
        var body = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        body!.Error.Should().BeNull(body.Error?.Message);
        return body.Result!.Value;
    }
}
