extern alias McpServer;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using McpServer::Mizan.Mcp.Server.Authorization;
using McpServer::Mizan.Mcp.Server.Services;
using Mizan.Contracts.Mcp;
using Moq;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The MCP endpoint as a client meets it: a 401 that says where to sign in, tools
/// limited to what the grant covers, and the same behavior for clients that
/// still send initialize and for those that do not.
/// </summary>
public class McpOAuthTransportTests : IClassFixture<WebApplicationFactory<McpServer::Program>>
{
    private const string PublicUrl = "https://mizan.example/mcp";
    private readonly WebApplicationFactory<McpServer::Program> _factory;
    private readonly Mock<IBackendApiClient> _backend = new();
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _grant = Guid.NewGuid();

    public McpOAuthTransportTests(WebApplicationFactory<McpServer::Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Mcp:ServiceApiKey", "test-api-key");
            builder.UseSetting("Mcp:AdminServiceApiKey", "test-admin-api-key");
            builder.UseSetting("Mcp:PublicUrl", PublicUrl);
            builder.UseSetting("Mcp:AuthorizationServer", "https://mizan.example/api");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MizanApiUrl"] = "http://localhost:5000",
                ["ServiceApiKey"] = "test-api-key",
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBackendApiClient>();
                services.AddSingleton(_backend.Object);
            });
        });
    }

    // ---- sign-in discovery ----

    [Fact]
    public async Task WithoutAToken_TheAnswerIs401_AndSaysWhereToSignIn()
    {
        using var client = _factory.CreateClient();

        var response = await RawAsync(client, "tools/list");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var challenge = response.Headers.WwwAuthenticate.Single();
        challenge.Scheme.Should().Be("Bearer");
        challenge.Parameter.Should().Contain("resource_metadata=").And.Contain("/mcp/.well-known/oauth-protected-resource");
    }

    [Fact]
    public async Task TheResourceMetadata_NamesTheResourceAndTheAuthorizationServer()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/mcp/.well-known/oauth-protected-resource");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        doc.GetProperty("resource").GetString().Should().Be(PublicUrl);
        doc.GetProperty("authorization_servers").EnumerateArray().Select(e => e.GetString())
            .Should().BeEquivalentTo(new[] { "https://mizan.example/api" });
        doc.GetProperty("scopes_supported").EnumerateArray().Select(e => e.GetString())
            .Should().Contain("nutrition:read").And.NotContain("admin");
    }

    [Fact]
    public async Task AnInvalidToken_IsRefusedWith401()
    {
        _backend.Setup(b => b.IntrospectAsync("bad", It.IsAny<CancellationToken>())).ReturnsAsync(TokenIntrospection.Inactive);
        using var client = ClientWith("bad");

        (await RawAsync(client, "tools/list")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WhenTheApiCannotBeReached_TheTokenIsNotTrusted()
    {
        _backend.Setup(b => b.IntrospectAsync("t", It.IsAny<CancellationToken>())).ReturnsAsync((TokenIntrospection?)null);
        using var client = ClientWith("t");

        (await RawAsync(client, "tools/list")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- what a grant can see and call ----

    [Fact]
    public async Task ToolsList_ShowsOnlyWhatTheGrantCovers()
    {
        using var client = Authorized("t", "nutrition:read");

        var names = await ToolNamesAsync(client);

        names.Should().Contain(new[] { "search_foods", "get_food_diary" });
        names.Should().NotContain(new[] { "log_food", "delete_meal", "get_workout", "ask_ai", "admin_list_users" });
        names.Select(n => McpToolScopes.All[n]).Should().OnlyContain(scope => scope.StartsWith("nutrition:"));
    }

    [Fact]
    public async Task WriteIncludesRead_AndAdminToolsNeedTheAdminScope()
    {
        using var writer = Authorized("w", "nutrition:write");
        var written = await ToolNamesAsync(writer);
        written.Should().Contain(new[] { "log_food", "search_foods" });
        written.Should().NotContain("admin_list_users");

        using var admin = Authorized("a", "nutrition:read", "admin");
        (await ToolNamesAsync(admin)).Should().Contain("admin_list_users");
    }

    [Fact]
    public async Task FullScope_ListsEveryToolExceptAdminOnes()
    {
        using var client = Authorized("f", McpScopes.Full);

        var names = await ToolNamesAsync(client);

        names.Should().NotContain(n => n.StartsWith("admin_"));
        names.Count.Should().Be(McpToolScopes.All.Count(kv => kv.Value != McpScopes.Admin));
    }

    [Fact]
    public async Task EveryRegisteredTool_HasAScope_AndEveryScopeIsReal()
    {
        using var client = Authorized("all", McpScopes.Full, McpScopes.Admin);

        var listed = await ToolNamesAsync(client);

        listed.Should().BeEquivalentTo(McpToolScopes.All.Keys,
            "a tool with no scope on record is invisible and uncallable, and a scope on record for no tool is stale");
        McpToolScopes.All.Values.Should().OnlyContain(scope => McpScopes.All.Contains(scope));
    }

    [Fact]
    public async Task EveryListedTool_HasATitle_AndIsMarkedClosedWorld()
    {
        using var client = Authorized("f", McpScopes.Full);

        var tools = (await ListAsync(client)).GetProperty("result").GetProperty("tools").EnumerateArray().ToList();

        tools.Select(t => t.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "").Should().OnlyContain(title => title.Length > 0);
        tools.Should().OnlyContain(t => t.GetProperty("annotations").GetProperty("openWorldHint").GetBoolean() == false);
    }

    [Fact]
    public async Task ACallOutsideTheGrant_IsRefusedWithTheExactPermission_AndNeverReachesTheApi()
    {
        using var client = Authorized("t", "nutrition:read");

        var result = await CallAsync(client, "log_food", new { foodId = Guid.NewGuid(), date = "2026-10-01", mealType = "LUNCH", servings = 1 });

        result.GetProperty("isError").GetBoolean().Should().BeTrue();
        var text = result.GetProperty("content")[0].GetProperty("text").GetString()!;
        text.Should().Contain("nutrition:write").And.Contain("Connected apps");
        _backend.Verify(b => b.PostAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Never);
        _backend.Verify(b => b.LogUsageAsync(_grant, _user, "tool", "log_food", false, It.IsAny<string?>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task AnAdminToolCall_WithoutTheAdminScope_IsRefused()
    {
        using var client = Authorized("t", McpScopes.Full);

        var result = await CallAsync(client, "admin_list_users", new { });

        result.GetProperty("isError").GetBoolean().Should().BeTrue();
        result.GetProperty("content")[0].GetProperty("text").GetString().Should().Contain("administrator");
        _backend.Verify(b => b.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnAllowedCall_ReachesTheApi_AndIsLoggedAgainstTheConnection()
    {
        _backend.Setup(b => b.GetAsync(It.Is<string>(s => s.Contains("/api/Foods/search")), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"items":[{"name":"Chicken"}]}""");
        using var client = Authorized("t", "nutrition:read");

        var result = await CallAsync(client, "search_foods", new { search = "chicken" });

        (result.TryGetProperty("isError", out var error) && error.GetBoolean()).Should().BeFalse();
        _backend.Verify(b => b.LogUsageAsync(_grant, _user, "tool", "search_foods", true, null, It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task TheFreePlanLimit_StillStopsCalls()
    {
        _backend.Setup(b => b.IntrospectAsync("t", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Introspection(new[] { McpScopes.Full }, limit: 15, used: 15));
        using var client = ClientWith("t");

        var result = await CallAsync(client, "search_foods", new { search = "x" });

        result.GetProperty("content")[0].GetProperty("text").GetString().Should().StartWith("[MONTHLY LIMIT REACHED]");
    }

    // ---- caching of the introspection ----

    [Fact]
    public async Task AnUnlimitedToken_IsIntrospectedOnce_ButALimitedOneEveryTime()
    {
        _backend.Setup(b => b.IntrospectAsync("free", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Introspection(new[] { "nutrition:read" }, limit: 15, used: 0));
        _backend.Setup(b => b.IntrospectAsync("pro", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Introspection(new[] { "nutrition:read" }));

        foreach (var token in new[] { "free", "pro" })
        {
            using var client = ClientWith(token);
            await ToolNamesAsync(client);
            await ToolNamesAsync(client);
        }

        _backend.Verify(b => b.IntrospectAsync("pro", It.IsAny<CancellationToken>()), Times.Once);
        _backend.Verify(b => b.IntrospectAsync("free", It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    // ---- protocol styles ----

    [Fact]
    public async Task InitializeClients_GetNoSession_AndTheServerInstructions()
    {
        using var client = Authorized("t", "nutrition:read");

        using var request = Build("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"legacy","version":"1"}}}""");
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Mcp-Session-Id").Should().BeFalse("the server is stateless");
        var body = await ReadJsonRpcAsync(response);
        body.GetProperty("result").GetProperty("instructions").GetString()
            .Should().Contain("servings").And.Contain("log_day");
    }

    [Fact]
    public async Task StatelessClients_CanCallWithoutAnyHandshake()
    {
        using var client = Authorized("t", "nutrition:read");

        // The 2026-07-28 revision: every request carries what the server needs to know.
        using var request = Build("""
            {"jsonrpc":"2.0","id":1,"method":"tools/list","params":{"_meta":{
              "io.modelcontextprotocol/protocolVersion":"2026-07-28",
              "io.modelcontextprotocol/clientCapabilities":{},
              "io.modelcontextprotocol/clientInfo":{"name":"stateless","version":"1"}}}}
            """);
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2026-07-28");
        request.Headers.TryAddWithoutValidation("Mcp-Method", "tools/list");
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonRpcAsync(response);
        body.GetProperty("result").GetProperty("tools").EnumerateArray().Select(t => t.GetProperty("name").GetString())
            .Should().Contain("search_foods").And.NotContain("log_food");
    }

    // ---- helpers ----

    private TokenIntrospection Introspection(string[] scopes, int? limit = null, int used = 0) => new()
    {
        Active = true, UserId = _user, GrantId = _grant, ClientRowId = Guid.NewGuid(), ClientName = "Test",
        Scopes = scopes, HouseholdMode = "all", Role = "user", Plan = limit is null ? "pro" : "free",
        MonthlyLimit = limit, UsedThisMonth = used,
    };

    private HttpClient ClientWith(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private HttpClient Authorized(string token, params string[] scopes)
    {
        _backend.Setup(b => b.IntrospectAsync(token, It.IsAny<CancellationToken>())).ReturnsAsync(Introspection(scopes));
        return ClientWith(token);
    }

    private static HttpRequestMessage Build(string json)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        message.Headers.Accept.ParseAdd("application/json");
        message.Headers.Accept.ParseAdd("text/event-stream");
        return message;
    }

    private static Task<HttpResponseMessage> RawAsync(HttpClient client, string method)
    {
        var request = Build($$"""{"jsonrpc":"2.0","id":1,"method":"{{method}}"}""");
        return client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadJsonRpcAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
        {
            text = text.Split('\n').First(l => l.StartsWith("data:") && l.Contains('{'))["data:".Length..].Trim();
        }

        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static async Task<JsonElement> ListAsync(HttpClient client)
    {
        var response = await client.PostMcpAsync(new { jsonrpc = "2.0", id = 2, method = "tools/list" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static async Task<List<string>> ToolNamesAsync(HttpClient client) =>
        (await ListAsync(client)).GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()!).ToList();

    private static async Task<JsonElement> CallAsync(HttpClient client, string tool, object arguments)
    {
        var response = await client.PostMcpAsync(new
        {
            jsonrpc = "2.0", id = 3, method = "tools/call", @params = new { name = tool, arguments },
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("result").Clone();
    }
}
