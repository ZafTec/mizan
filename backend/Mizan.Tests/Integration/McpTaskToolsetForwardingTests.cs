extern alias McpServer;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using McpServer::Mizan.Mcp.Server.Services;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// With <c>Mcp:Toolset=tasks</c> the server lists the API's task-tool manifest and forwards each call to the
/// API's runner. The API decides what a grant may reach; the server maps the result and keeps the usage log.
/// </summary>
public class McpTaskToolsetForwardingTests : IClassFixture<WebApplicationFactory<McpServer::Program>>
{
    private readonly WebApplicationFactory<McpServer::Program> _factory;
    private readonly StubApi _api = new();
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _grant = Guid.NewGuid();

    public McpTaskToolsetForwardingTests(WebApplicationFactory<McpServer::Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Mcp:ServiceApiKey", "test-api-key");
            builder.UseSetting("Mcp:AdminServiceApiKey", "test-admin-api-key");
            builder.UseSetting("Mcp:Toolset", "tasks");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MizanApiUrl"] = "http://localhost:5000",
                ["ServiceApiKey"] = "test-api-key",
            }));
            builder.ConfigureTestServices(services =>
                services.AddHttpClient<IBackendApiClient, BackendApiClient>().ConfigurePrimaryHttpMessageHandler(() => _api));
        });
        _api.Introspection = () => new
        {
            active = true,
            userId = _user,
            grantId = _grant,
            clientRowId = Guid.NewGuid(),
            clientId = "c",
            clientName = "Test",
            scopes = new[] { "nutrition:read", "nutrition:write" },
            householdMode = "all",
            householdIds = Array.Empty<Guid>(),
            role = "user",
            plan = "pro",
        };
        _api.Reply("GET", "/api/tools", 200, """
            {"surface":"mcp","tools":[
              {"name":"get_nutrition","title":"Nutrition","description":"d","kind":"read","inputSchema":{"type":"object"},"app":"ui://mizan/nutrition-day.html"},
              {"name":"delete_food_entry","title":"Delete","description":"d","kind":"destructive","inputSchema":{"type":"object"},"app":null}]}
            """);
    }

    [Fact]
    public async Task A_tasks_connection_lists_the_apis_manifest_and_a_call_is_forwarded_as_the_person_and_logged()
    {
        _api.Reply("POST", "/api/tools/get_nutrition/call", 200, """
            {"tool":"get_nutrition","mode":"day","isError":false,"text":"1,800 kcal","data":{"totalCalories":1800},"errorCode":null,"needs":null,"confirmation":null}
            """);
        using var client = Authorized();

        var listed = Result(await SendAsync(client, "tools/list", null)).GetProperty("tools").EnumerateArray()
            .ToDictionary(t => t.GetProperty("name").GetString()!);
        listed.Keys.Should().BeEquivalentTo("get_nutrition", "delete_food_entry");
        listed["get_nutrition"].GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean().Should().BeTrue();
        listed["get_nutrition"].GetProperty("_meta").GetProperty("ui").GetProperty("resourceUri").GetString().Should().Be("ui://mizan/nutrition-day.html");
        listed["delete_food_entry"].GetProperty("annotations").GetProperty("destructiveHint").GetBoolean().Should().BeTrue();

        var called = Result(await SendAsync(client, "tools/call", new { name = "get_nutrition", arguments = new { view = "day" } }));

        called.GetProperty("content")[0].GetProperty("text").GetString().Should().Be("1,800 kcal");
        called.GetProperty("structuredContent").GetProperty("totalCalories").GetInt32().Should().Be(1800);
        var forwarded = await _api.WaitForAsync("POST", "/api/tools/get_nutrition/call");
        forwarded.Impersonating.Should().Be(_user.ToString());
        forwarded.Grant.Should().Be(_grant.ToString());
        JsonNode.Parse(forwarded.Body)!["surface"]!.GetValue<string>().Should().Be("mcp");
        (await _api.WaitForAsync("POST", "/api/McpConnections/usage")).Body.Should().Contain("get_nutrition");
    }

    [Fact]
    public async Task A_destructive_call_without_a_token_returns_the_preview_and_its_token_in_structured_content()
    {
        _api.Reply("POST", "/api/tools/delete_food_entry/call", 200, """
            {"tool":"delete_food_entry","mode":null,"isError":false,"text":"Deletes 1 entry. Confirm with token abc.",
             "data":{"preview":{"count":1},"confirmToken":"abc","expiresAt":"2026-11-07T10:05:00Z"},"errorCode":null,"needs":null,
             "confirmation":{"token":"abc","expiresAt":"2026-11-07T10:05:00Z","preview":{"count":1}}}
            """);
        using var client = Authorized();

        var called = Result(await SendAsync(client, "tools/call", new { name = "delete_food_entry", arguments = new { id = Guid.NewGuid() } }));

        var confirmation = called.GetProperty("structuredContent").GetProperty("confirmation");
        confirmation.GetProperty("token").GetString().Should().Be("abc");
        confirmation.GetProperty("preview").GetProperty("count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task The_reference_lists_and_app_pages_are_free_and_the_pages_use_the_task_tools()
    {
        _api.Reply("GET", "/api/tools/reference", 200, """{"priceRegions":["DE","US"],"goalTypes":[],"mealSlots":[],"macroModes":[],"reminderKinds":[]}""");
        using var client = Authorized();

        var regions = Result(await SendAsync(client, "resources/read", new { uri = "mizan://reference/price-regions" }))
            .GetProperty("contents")[0].GetProperty("text").GetString();
        var page = Result(await SendAsync(client, "resources/read", new { uri = "ui://mizan/nutrition-day.html" }))
            .GetProperty("contents")[0].GetProperty("text").GetString();

        regions.Should().Contain("DE");
        page.Should().Contain("const toolset = \"tasks\"");
        _api.Requests.Should().NotContain(r => r.Path == "/api/McpConnections/usage", "reference lists and pages hold no personal data");
    }

    private HttpClient Authorized()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token");
        return client;
    }

    private static JsonElement Result(string body)
    {
        var root = JsonDocument.Parse(body).RootElement;
        root.TryGetProperty("error", out var error).Should().BeFalse(error.ToString());
        return root.GetProperty("result");
    }

    private static async Task<string> SendAsync(HttpClient client, string method, object? @params)
    {
        var node = JsonSerializer.SerializeToNode(@params ?? new { })!.AsObject();
        node["_meta"] = JsonSerializer.SerializeToNode(new Dictionary<string, object>
        {
            ["io.modelcontextprotocol/protocolVersion"] = "2026-07-28",
            ["io.modelcontextprotocol/clientInfo"] = new { name = "test", version = "1" },
            ["io.modelcontextprotocol/clientCapabilities"] = new { },
        });
        var json = JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 1, method, @params = node });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2026-07-28");
        request.Headers.TryAddWithoutValidation("Mcp-Method", method);
        var named = node["name"] ?? node["uri"];
        if (named is not null) request.Headers.TryAddWithoutValidation("Mcp-Name", named.GetValue<string>());
        var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
            text = text.Split('\n').First(l => l.StartsWith("data:") && l.Contains('{'))["data:".Length..].Trim();
        return text;
    }
}
