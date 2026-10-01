extern alias McpServer;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using McpServer::Mizan.Mcp.Server.Services;
using Mizan.Contracts.Mcp;
using Moq;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The two extensions on top of tools: small interactive views a host can show
/// (Apps), and long-running calls a client can start and poll (Tasks). Both run
/// under the same grant as everything else, so a connection never gains reach by
/// using them.
/// </summary>
public class McpAppsAndTasksTests : IClassFixture<WebApplicationFactory<McpServer::Program>>
{
    private readonly WebApplicationFactory<McpServer::Program> _factory;
    private readonly StubApi _api = new();
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _grant = Guid.NewGuid();
    private string[] _scopes = [McpScopes.Full];

    public McpAppsAndTasksTests(WebApplicationFactory<McpServer::Program> factory)
    {
        // The real API client talks to a stub, so what the API would see (who is asking, under which
        // grant) is checked as it goes over the wire, including from background work.
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Mcp:ServiceApiKey", "test-api-key");
            builder.UseSetting("Mcp:AdminServiceApiKey", "test-admin-api-key");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MizanApiUrl"] = "http://localhost:5000",
                ["ServiceApiKey"] = "test-api-key",
            }));
            builder.ConfigureTestServices(services =>
                services.AddHttpClient<IBackendApiClient, BackendApiClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => _api));
        });
        _api.Introspection = () => new
        {
            active = true, userId = _user, grantId = _grant, clientRowId = Guid.NewGuid(), clientId = "c", clientName = "Test",
            scopes = _scopes, householdMode = "all", householdIds = Array.Empty<Guid>(), role = "user", plan = "pro",
        };
    }

    // ---- apps ----

    [Fact]
    public async Task TheAppPages_AreListedAsUiResources_WithTheAppMimeType()
    {
        using var client = Authorized();

        var (status, body) = await SendAsync(client, "resources/list", null);

        status.Should().Be(200);
        var resources = JsonDocument.Parse(body).RootElement.GetProperty("result").GetProperty("resources").EnumerateArray()
            .Where(r => r.GetProperty("uri").GetString()!.StartsWith("ui://")).ToList();
        resources.Select(r => r.GetProperty("uri").GetString()).Should().BeEquivalentTo(
            "ui://mizan/nutrition-day.html", "ui://mizan/food-photo.html", "ui://mizan/body-trend.html");
        resources.Should().OnlyContain(r => r.GetProperty("mimeType").GetString() == "text/html;profile=mcp-app");
    }

    [Fact]
    public async Task AnAppPage_NeedsTheScopeOfItsTool_SoItIsHiddenWithoutIt()
    {
        _scopes = ["body:read"];
        using var client = Authorized();

        var (_, body) = await SendAsync(client, "resources/list", null);

        JsonDocument.Parse(body).RootElement.GetProperty("result").GetProperty("resources").EnumerateArray()
            .Select(r => r.GetProperty("uri").GetString()!).Where(u => u.StartsWith("ui://"))
            .Should().BeEquivalentTo("ui://mizan/body-trend.html");
    }

    [Fact]
    public async Task ReadingAnAppPage_ReturnsSelfContainedHtml_AndDoesNotCountAgainstTheMonthlyCap()
    {
        using var client = Authorized();

        var (status, body) = await SendAsync(client, "resources/read", new { uri = "ui://mizan/nutrition-day.html" });

        status.Should().Be(200);
        var content = JsonDocument.Parse(body).RootElement.GetProperty("result").GetProperty("contents")[0];
        content.GetProperty("mimeType").GetString().Should().Be("text/html;profile=mcp-app");
        var html = content.GetProperty("text").GetString()!;
        html.Should().Contain("ui/initialize").And.Contain("get_nutrition_summary");
        // The only address allowed is the SVG namespace, which is a name and not a request.
        html.Replace("http://www.w3.org/2000/svg", "").Should().NotContain("http://").And.NotContain("https://",
            "the host blocks network access, so a page must carry everything it needs");
        _api.Requests.Should().NotContain(r => r.Path == "/api/McpConnections/usage", "app pages hold no data, like skills");
    }

    [Fact]
    public async Task AnAppPage_IsRefusedWithoutItsScope()
    {
        _scopes = ["body:read"];
        using var client = Authorized();

        var (_, body) = await SendAsync(client, "resources/read", new { uri = "ui://mizan/food-photo.html" });

        JsonDocument.Parse(body).RootElement.TryGetProperty("error", out _).Should().BeTrue();
    }

    [Fact]
    public async Task TheToolsThatHaveAnApp_PointAtIt()
    {
        using var client = Authorized();

        var (_, body) = await SendAsync(client, "tools/list", null);

        var tools = JsonDocument.Parse(body).RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .ToDictionary(t => t.GetProperty("name").GetString()!);
        string Ui(string tool) => tools[tool].GetProperty("_meta").GetProperty("ui").GetProperty("resourceUri").GetString()!;
        Ui("get_nutrition_summary").Should().Be("ui://mizan/nutrition-day.html");
        Ui("analyze_food_image").Should().Be("ui://mizan/food-photo.html");
        Ui("list_body_measurements").Should().Be("ui://mizan/body-trend.html");
    }

    // ---- tasks ----

    [Fact]
    public async Task AnAiCall_BecomesATask_ThatFinishesInTheBackground_AsTheSameUser()
    {
        _api.Reply("POST", "/api/Ai/chat", 200, """{"reply":"hi"}""");
        using var client = Authorized();

        var (status, body) = await SendAsync(client, "tools/call", new { name = "ask_ai", arguments = new { message = "hello" } }, withTasks: true);

        status.Should().Be(200);
        var created = JsonDocument.Parse(body).RootElement.GetProperty("result");
        created.GetProperty("resultType").GetString().Should().Be("task");
        created.GetProperty("taskId").GetString().Should().Be(StubApi.TaskId);

        var complete = await _api.WaitForAsync("POST", $"/api/McpTasks/{StubApi.TaskId}/complete");
        complete.Body.Should().Contain("hi");
        // Nothing of the request is left by now. The work still acts as the user who asked, under their grant.
        new[] { complete }.Concat(_api.Requests.Where(r => r.Path == "/api/Ai/chat" || r.Path == "/api/McpTasks"))
            .Should().OnlyContain(r => r.Impersonating == _user.ToString() && r.Grant == _grant.ToString());
        var usage = await _api.WaitForAsync("POST", "/api/McpConnections/usage");
        usage.Body.Should().Contain("ask_ai").And.Contain("\"success\":true");
    }

    [Fact]
    public async Task ATaskThatFails_IsRecordedAsFailed_AndSettlesTheUsage()
    {
        _api.Reply("POST", "/api/Ai/chat", 500, """{"error":"provider down"}""");
        using var client = Authorized();

        await SendAsync(client, "tools/call", new { name = "ask_ai", arguments = new { message = "hello" } }, withTasks: true);

        var usage = await _api.WaitForAsync("POST", "/api/McpConnections/usage");
        usage.Body.Should().Contain("ask_ai").And.Contain("\"success\":false");
        _api.Requests.Count(r => r.Path.StartsWith($"/api/McpTasks/{StubApi.TaskId}/") && r.Method == "POST")
            .Should().BeLessThanOrEqualTo(1, "a task is finished once");
    }

    [Fact]
    public async Task WithoutTheTasksExtension_TheSameCallRunsInline()
    {
        _api.Reply("POST", "/api/Ai/chat", 200, """{"reply":"inline"}""");
        using var client = Authorized();

        var (_, body) = await SendAsync(client, "tools/call", new { name = "ask_ai", arguments = new { message = "hello" } }, withTasks: false);

        var result = JsonDocument.Parse(body).RootElement.GetProperty("result");
        result.TryGetProperty("taskId", out _).Should().BeFalse();
        result.GetProperty("content")[0].GetProperty("text").GetString().Should().Contain("inline");
        _api.Requests.Should().NotContain(r => r.Path == "/api/McpTasks");
    }

    [Fact]
    public async Task AToolNotMeantForTasks_RunsInline_EvenForAClientThatAsksForOne()
    {
        _api.Reply("GET", "/api/Nutrition/daily", 200, """{"calories":1200}""");
        using var client = Authorized();

        var (_, body) = await SendAsync(client, "tools/call", new { name = "get_nutrition_summary", arguments = new { } }, withTasks: true);

        var result = JsonDocument.Parse(body).RootElement.GetProperty("result");
        result.TryGetProperty("taskId", out _).Should().BeFalse();
        result.GetProperty("content")[0].GetProperty("text").GetString().Should().Contain("1200");
    }

    [Fact]
    public async Task ACallTheGrantDoesNotCover_IsRefusedAtOnce_NotTurnedIntoATask()
    {
        _scopes = ["nutrition:read"];
        using var client = Authorized();

        var (_, body) = await SendAsync(client, "tools/call", new { name = "ask_ai", arguments = new { message = "hello" } }, withTasks: true);

        var result = JsonDocument.Parse(body).RootElement.GetProperty("result");
        result.TryGetProperty("taskId", out _).Should().BeFalse();
        result.GetProperty("isError").GetBoolean().Should().BeTrue();
        result.GetProperty("content")[0].GetProperty("text").GetString().Should().Contain("ai:use");
        _api.Requests.Should().NotContain(r => r.Path == "/api/McpTasks" || r.Path == "/api/Ai/chat");
    }

    [Fact]
    public async Task PollingATask_ReportsWhatTheApiHolds()
    {
        _api.TaskState = StubApi.TaskJson("completed", """{"content":[{"type":"text","text":"done"}]}""");
        using var client = Authorized();

        var (status, body) = await SendAsync(client, "tasks/get", new { taskId = StubApi.TaskId }, withTasks: true);

        status.Should().Be(200);
        var result = JsonDocument.Parse(body).RootElement.GetProperty("result");
        result.GetProperty("status").GetString().Should().Be("completed");
        result.ToString().Should().Contain("done");
    }

    [Fact]
    public async Task APollForATaskThatIsNotTheirs_FindsNothing()
    {
        _api.TaskState = null;
        using var client = Authorized();

        var (_, body) = await SendAsync(client, "tasks/get", new { taskId = "someone-elses" }, withTasks: true);

        JsonDocument.Parse(body).RootElement.TryGetProperty("error", out _).Should().BeTrue();
    }

    // ---- helpers ----

    private HttpClient Authorized()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token");
        return client;
    }

    private static async Task<(int Status, string Body)> SendAsync(HttpClient client, string method, object? @params, bool withTasks = false)
    {
        var meta = new Dictionary<string, object>
        {
            ["io.modelcontextprotocol/protocolVersion"] = "2026-07-28",
            ["io.modelcontextprotocol/clientInfo"] = new { name = "test", version = "1" },
            ["io.modelcontextprotocol/clientCapabilities"] = withTasks
                ? new { extensions = new Dictionary<string, object> { ["io.modelcontextprotocol/tasks"] = new { } } }
                : new { },
        };
        var node = JsonSerializer.SerializeToNode(@params ?? new { })!.AsObject();
        node["_meta"] = JsonSerializer.SerializeToNode(meta);
        var json = JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 1, method, @params = node });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2026-07-28");
        request.Headers.TryAddWithoutValidation("Mcp-Method", method);
        var named = node["name"] ?? node["uri"] ?? node["taskId"];
        if (named is not null && method is "tools/call" or "resources/read" or "prompts/get" or "tasks/get") request.Headers.TryAddWithoutValidation("Mcp-Name", named.GetValue<string>());
        var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
            text = text.Split('\n').First(l => l.StartsWith("data:") && l.Contains('{'))["data:".Length..].Trim();
        return ((int)response.StatusCode, text);
    }
}

/// <summary>The API as the MCP service sees it. Records every request so a test can say who was asking.</summary>
internal sealed class StubApi : HttpMessageHandler
{
    public const string TaskId = "0123456789abcdef0123456789abcdef";

    public sealed record Seen(string Method, string Path, string Body, string? Impersonating, string? Grant);

    private readonly List<Seen> _requests = [];
    private readonly Dictionary<string, (int Status, string Body)> _replies = new();
    public Func<object> Introspection { get; set; } = () => new { active = false };
    public string? TaskState { get; set; } = TaskJson("working");

    public IReadOnlyList<Seen> Requests { get { lock (_requests) return _requests.ToList(); } }

    public static string TaskJson(string status, string? result = null) =>
        $$"""{"id":"{{TaskId}}","status":"{{status}}","createdAt":"2026-10-01T10:00:00Z","updatedAt":"2026-10-01T10:00:00Z","ttlSeconds":3600,"pollIntervalMs":2000{{(result is null ? "" : ",\"result\":" + result)}}}""";

    public void Reply(string method, string path, int status, string body) => _replies[$"{method} {path}"] = (status, body);

    public async Task<Seen> WaitForAsync(string method, string path)
    {
        for (var i = 0; i < 100; i++)
        {
            var found = Requests.FirstOrDefault(r => r.Method == method && r.Path == path);
            if (found is not null) return found;
            await Task.Delay(50);
        }

        throw new TimeoutException($"No {method} {path}. Saw: {string.Join(", ", Requests.Select(r => r.Method + " " + r.Path))}");
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var method = request.Method.Method;
        lock (_requests)
            _requests.Add(new Seen(method, path, body,
                request.Headers.TryGetValues("X-Impersonate-User", out var user) ? user.First() : null,
                request.Headers.TryGetValues("X-Mcp-Grant", out var grant) ? grant.First() : null));

        static HttpResponseMessage Json(int status, string json) =>
            new((System.Net.HttpStatusCode)status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        if (path == "/api/oauth/introspect") return Json(200, JsonSerializer.Serialize(Introspection()));
        if (_replies.TryGetValue($"{method} {path}", out var reply)) return Json(reply.Status, reply.Body);
        if (path == "/api/McpTasks" && method == "POST") return Json(201, TaskJson("working"));
        if (path == $"/api/McpTasks/{TaskId}" && method == "GET" || path.StartsWith("/api/McpTasks/") && method == "GET")
            return TaskState is not null && path.EndsWith(TaskId) ? Json(200, TaskState) : new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        if (path.StartsWith("/api/McpTasks/") && method == "POST") return new HttpResponseMessage(System.Net.HttpStatusCode.NoContent);
        if (path == "/api/McpConnections/usage") return new HttpResponseMessage(System.Net.HttpStatusCode.NoContent);
        return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
    }
}
