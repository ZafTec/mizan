extern alias McpServer;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using McpServer::Mizan.Mcp.Server.Services;
using Mizan.Contracts.Prices;
using Mizan.Domain.Entities;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The price tools are thin: each is one API call. This walks an
/// administrator through the review queue and a person through a report,
/// calling every price tool once through the real API.
/// </summary>
[Collection("ApiIntegration")]
public class PriceMcpTests : IClassFixture<WebApplicationFactory<McpServer::Program>>
{
    private readonly HttpClient _mcp;
    private readonly ApiTestFixture _api;
    private readonly PlanningWorld _world;

    public PriceMcpTests(WebApplicationFactory<McpServer::Program> mcpFactory, ApiTestFixture api)
    {
        _api = api;
        _world = new PlanningWorld(api);
        var settings = new Dictionary<string, string?>
        {
            ["MizanApiUrl"] = "http://localhost:5000",
            ["Mcp:ServiceApiKey"] = "test-api-key",
            ["Mcp:AdminServiceApiKey"] = "test-admin-api-key",
        };
        _mcp = mcpFactory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings) builder.UseSetting(key, value);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBackendApiClient>();
                services.AddScoped<IBackendApiClient>(sp => new BackendApiClient(
                    new HttpClient(_api.Server.CreateHandler()) { BaseAddress = new Uri("http://localhost:5000") },
                    sp.GetRequiredService<IHttpContextAccessor>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BackendApiClient>>(),
                    new ConfigurationBuilder().AddInMemoryCollection(settings).Build()));
            });
        }).CreateClient();
    }

    [Fact]
    public async Task AnAdministratorWorksTheReviewQueueThroughMcp_AndAPersonReportsAPrice()
    {
        await _api.ResetDatabaseAsync();
        var (adminId, admin) = await _world.PersonAsync("admin", role: "admin");
        var milk = await _world.FoodAsync("Milk", 61, 3.2m, 4.8m, 3.3m);
        var source = await _world.DbAsync(async db =>
        {
            var row = new PriceSource { Id = Guid.NewGuid(), Name = "Shop", Kind = Domain.Pricing.PriceSourceKind.Scraper, ScraperKey = "shop", RegionCode = "ET-AA", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            db.PriceSources.Add(row);
            db.PriceSourceAliases.Add(new PriceSourceAlias { Id = Guid.NewGuid(), SourceId = row.Id, ProductName = "Fresh milk 1L", LastSeenPrice = 95, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return row;
        });

        await AuthenticateAsync(adminId);
        (await CallAsync("admin_list_price_sources", new { })).Should().Contain("\"name\":\"Shop\"").And.Contain("\"unresolvedCount\":1");
        var queue = JsonDocument.Parse(await CallAsync("admin_list_price_review_queue", new { sourceId = source.Id.ToString() })).RootElement;
        var aliasId = queue.GetProperty("items")[0].GetProperty("id").GetString()!;
        await CallAsync("admin_update_price_alias", new { aliasId, foodId = milk.Id.ToString(), quantity = 1, unit = "l" });
        (await _world.DbAsync(db => db.PriceSourceAliases.SingleAsync())).FoodId.Should().Be(milk.Id);
        (await CallAsync("admin_run_price_source", new { sourceId = source.Id.ToString() })).Should().Contain("Prices:WorkerUrl");
        await CallAsync("admin_add_manual_price", new { foodId = milk.Id.ToString(), regionCode = "ET-AA", price = 90, quantity = 1, unit = "l" });

        var personId = Guid.NewGuid();
        await _api.SeedUserAsync(personId, $"person-{personId:N}@example.com");
        await AuthenticateAsync(personId);
        (await CallAsync("get_food_price", new { foodId = milk.Id.ToString() })).Should().Contain("\"confidence\":\"Estimated\"").And.Contain("\"pricePerKg\":90");
        await CallAsync("report_food_price", new { foodId = milk.Id.ToString(), suggestedPrice = 100, quantity = 1, unit = "l" });

        await AuthenticateAsync(adminId);
        var reports = JsonDocument.Parse(await CallAsync("admin_list_price_reports", new { })).RootElement;
        var reportId = reports.GetProperty("items")[0].GetProperty("id").GetString()!;
        await CallAsync("admin_resolve_price_report", new { reportId, action = "accept" });

        await AuthenticateAsync(personId);
        (await CallAsync("get_food_price", new { foodId = milk.Id.ToString() })).Should().Contain("\"confidence\":\"Reported\"");
    }

    private async Task AuthenticateAsync(Guid userId)
    {
        var token = (await _api.CreateMcpAccessAsync(userId)).Token;
        _mcp.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> CallAsync(string tool, object arguments)
    {
        var response = await _mcp.PostMcpAsync(new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString("N"),
            method = "tools/call",
            @params = new { name = tool, arguments },
        });
        var body = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        if (body?.Error is not null) return body.Error.Message;
        var result = JsonSerializer.Deserialize<JsonElement>(body!.Result!.ToString()!);
        return string.Join("\n", result.GetProperty("content").EnumerateArray()
            .Where(block => block.TryGetProperty("text", out _))
            .Select(block => block.GetProperty("text").GetString()));
    }
}
