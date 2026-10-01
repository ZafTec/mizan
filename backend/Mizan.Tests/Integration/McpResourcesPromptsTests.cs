extern alias McpServer;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
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
/// Resources, prompts and skills as a client meets them. Each is held to the
/// connection's grant exactly as tools are, and skills are the one public part.
/// </summary>
public class McpResourcesPromptsTests : IClassFixture<WebApplicationFactory<McpServer::Program>>
{
    private readonly WebApplicationFactory<McpServer::Program> _factory;
    private readonly Mock<IBackendApiClient> _backend = new();
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _grant = Guid.NewGuid();

    public McpResourcesPromptsTests(WebApplicationFactory<McpServer::Program> factory)
    {
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
            {
                services.RemoveAll<IBackendApiClient>();
                services.AddSingleton(_backend.Object);
            });
        });
    }

    // ---- resources ----

    [Fact]
    public async Task ResourcesAndTemplates_ShowOnlyWhatTheGrantCovers()
    {
        using var client = Authorized("nutrition:read");

        var resources = Names(await RpcAsync(client, "resources/list"), "resources", "uri");
        resources.Should().Contain(new[] { "mizan://goals/current", "mizan://nutrition/today" });
        resources.Should().NotContain(new[] { "mizan://profile", "mizan://households", "mizan://streak" });
        resources.Where(u => u.StartsWith("skill://")).Should().NotBeEmpty("skills need no permission");

        var templates = Names(await RpcAsync(client, "resources/templates/list"), "resourceTemplates", "uriTemplate");
        templates.Should().BeEquivalentTo(new[] { "mizan://diary/{date}", "mizan://nutrition/{date}" });
    }

    [Fact]
    public async Task AFullConnection_ListsEveryResourceFamily()
    {
        using var client = Authorized(McpScopes.Full);

        Names(await RpcAsync(client, "resources/list"), "resources", "uri")
            .Where(u => u.StartsWith("mizan://")).Should().BeEquivalentTo(new[]
            {
                "mizan://profile", "mizan://goals/current", "mizan://nutrition/today", "mizan://streak", "mizan://households",
            });
        Names(await RpcAsync(client, "resources/templates/list"), "resourceTemplates", "uriTemplate").Should().BeEquivalentTo(new[]
        {
            "mizan://diary/{date}", "mizan://nutrition/{date}", "mizan://recipes/{id}", "mizan://workouts/{id}",
            "mizan://meal-plans/{id}", "mizan://shopping-lists/{id}",
        });
    }

    [Fact]
    public async Task ReadingAResource_GoesThroughTheSameApiCall_AndIsLogged()
    {
        _backend.Setup(b => b.GetAsync(It.Is<string>(s => s.StartsWith("/api/Meals") && s.Contains("2026-10-01")), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"date":"2026-10-01","entries":[]}""");
        using var client = Authorized("nutrition:read");

        var result = await RpcAsync(client, "resources/read", new { uri = "mizan://diary/2026-10-01" });

        result.GetProperty("result").GetProperty("contents")[0].GetProperty("text").GetString().Should().Contain("2026-10-01");
        _backend.Verify(b => b.LogUsageAsync(_grant, _user, "resource", "mizan://diary/{date}", true, null, It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task ReadingAResourceOutsideTheGrant_IsRefused_AndNeverReachesTheApi()
    {
        using var client = Authorized("nutrition:read");

        var result = await RpcAsync(client, "resources/read", new { uri = "mizan://workouts/abc" });

        result.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetProperty("message").GetString().Should().Contain("training:read");
        _backend.Verify(b => b.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _backend.Verify(b => b.LogUsageAsync(_grant, _user, "resource", "mizan://workouts/{id}", false, It.IsAny<string?>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task AnUnknownResourceAddress_IsRefused()
    {
        using var client = Authorized(McpScopes.Full);

        (await RpcAsync(client, "resources/read", new { uri = "mizan://secrets/all" })).TryGetProperty("error", out _).Should().BeTrue();
        (await RpcAsync(client, "resources/read", new { uri = "file:///etc/passwd" })).TryGetProperty("error", out _).Should().BeTrue();
    }

    [Fact]
    public async Task TheFreePlanCap_AppliesToResources_ButNotToSkills()
    {
        _backend.Setup(b => b.IntrospectAsync("t", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Introspection(new[] { McpScopes.Full }, limit: 15, used: 15));
        using var client = ClientWith("t");

        var blocked = await RpcAsync(client, "resources/read", new { uri = "mizan://profile" });
        blocked.GetProperty("error").GetProperty("message").GetString().Should().StartWith("[MONTHLY LIMIT REACHED]");

        var skill = McpSkillCatalog.All.First();
        var allowed = await RpcAsync(client, "resources/read", new { uri = skill.Uri });
        allowed.GetProperty("result").GetProperty("contents")[0].GetProperty("text").GetString().Should().StartWith("---");
    }

    // ---- prompts ----

    [Fact]
    public async Task Prompts_ShowOnlyThoseWhoseToolsTheGrantCovers()
    {
        using var reader = Authorized("nutrition:read");
        Names(await RpcAsync(reader, "prompts/list"), "prompts", "name")
            .Should().BeEquivalentTo(new[] { "weekly_review", "macro_rescue" });

        using var full = Authorized(McpScopes.Full);
        Names(await RpcAsync(full, "prompts/list"), "prompts", "name").Should().BeEquivalentTo(new[]
        {
            "log_my_day", "plan_my_week", "grocery_run", "weekly_review", "workout_planner", "macro_rescue",
            "recipe_from_leftovers", "trainer_checkin",
        });
    }

    [Fact]
    public async Task EveryPrompt_HasAScope_AndOnlyNamesToolsThatExist()
    {
        using var client = Authorized(McpScopes.Full);
        var prompts = (await RpcAsync(client, "prompts/list")).GetProperty("result").GetProperty("prompts").EnumerateArray().ToList();
        prompts.Select(p => p.GetProperty("name").GetString()!).Should().BeEquivalentTo(McpResourceScopes.PromptScopes.Keys);

        foreach (var name in McpResourceScopes.PromptScopes.Keys)
        {
            var declared = prompts.Single(p => p.GetProperty("name").GetString() == name);
            var args = declared.TryGetProperty("arguments", out var list)
                ? list.EnumerateArray().Select(x => x.GetProperty("name").GetString()!).ToDictionary(n => n, n => n == "daysPerWeek" ? "3" : "sample")
                : new Dictionary<string, string>();

            var body = await RpcAsync(client, "prompts/get", new { name, arguments = args });
            var text = body.GetProperty("result").GetProperty("messages")[0].GetProperty("content").GetProperty("text").GetString()!;

            foreach (var word in ToolLikeWords(text))
                McpToolScopes.All.Should().ContainKey(word, $"prompt {name} names the tool {word}");
        }
    }

    [Fact]
    public async Task GettingAPrompt_FillsInTheArguments_AndIsRefusedWithoutTheScope()
    {
        using var writer = Authorized("nutrition:write");
        var ok = await RpcAsync(writer, "prompts/get", new { name = "log_my_day", arguments = new { description = "oats and a coffee", date = "2026-10-01" } });
        var text = ok.GetProperty("result").GetProperty("messages")[0].GetProperty("content").GetProperty("text").GetString()!;
        text.Should().Contain("oats and a coffee").And.Contain("2026-10-01").And.Contain("not grams");

        using var reader = Authorized("nutrition:read");
        (await RpcAsync(reader, "prompts/get", new { name = "log_my_day", arguments = new { description = "x" } }))
            .TryGetProperty("error", out _).Should().BeTrue();
    }

    // ---- skills ----

    [Fact]
    public async Task TheServer_AdvertisesTheSkillsExtension_AndResources()
    {
        using var client = Authorized("profile:read");

        var init = await RpcAsync(client, "initialize", new
        {
            protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "t", version = "1" },
        });

        init.ToString().Should().Contain("\"capabilities\"");
        var capabilities = init.GetProperty("result").GetProperty("capabilities");
        capabilities.ToString().Should().Contain("io.modelcontextprotocol/skills");
        capabilities.TryGetProperty("resources", out _).Should().BeTrue();
        capabilities.GetProperty("extensions").TryGetProperty("io.modelcontextprotocol/skills", out _).Should().BeTrue();
    }

    [Fact]
    public async Task SkillsList_ReturnsEverySkill_WithAManifestThatMatchesWhatIsServed()
    {
        using var client = Authorized("profile:read");

        var list = (await RpcAsync(client, "skills/list")).GetProperty("result");
        list.GetProperty("resultType").GetString().Should().Be("complete");
        list.GetProperty("cacheScope").GetString().Should().Be("public");

        var skills = list.GetProperty("skills").EnumerateArray().ToList();
        skills.Select(s => s.GetProperty("frontmatter").GetProperty("name").GetString())
            .Should().BeEquivalentTo(new[]
            {
                "mizan-food-logging", "mizan-recipes", "mizan-meal-planning", "mizan-training", "mizan-progress-review", "mizan-coaching",
            });

        foreach (var skill in skills)
        {
            var name = skill.GetProperty("frontmatter").GetProperty("name").GetString()!;
            skill.GetProperty("uri").GetString().Should().Be($"skill://{name}/SKILL.md");
            skill.GetProperty("frontmatter").GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();

            foreach (var file in skill.GetProperty("resources").EnumerateArray())
            {
                var uri = file.GetProperty("uri").GetString()!;
                var read = await RpcAsync(client, "resources/read", new { uri });
                var content = read.GetProperty("result").GetProperty("contents")[0];
                content.GetProperty("mimeType").GetString().Should().Be("text/markdown");

                var bytes = Encoding.UTF8.GetBytes(content.GetProperty("text").GetString()!);
                file.GetProperty("size").GetInt32().Should().Be(bytes.Length, uri);
                file.GetProperty("digest").GetString().Should().Be("sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes)), uri);
            }
        }
    }

    [Fact]
    public async Task SkillsGet_ReturnsOneSkill_AndRefusesAnUnknownOne()
    {
        using var client = Authorized("profile:read");

        var found = (await RpcAsync(client, "skills/get", new { uri = "skill://mizan-recipes/SKILL.md" })).GetProperty("result");
        found.GetProperty("skill").GetProperty("frontmatter").GetProperty("name").GetString().Should().Be("mizan-recipes");

        var missing = await RpcAsync(client, "skills/get", new { uri = "skill://nope/SKILL.md" });
        missing.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32602);
    }

    [Fact]
    public void EverySkill_IsWellFormed()
    {
        McpSkillCatalog.All.Should().HaveCount(6);
        foreach (var skill in McpSkillCatalog.All)
        {
            skill.Frontmatter["name"].Should().Be(skill.Name, "the folder name must equal the skill name");
            skill.Frontmatter["description"].Length.Should().BeInRange(40, 1024);
            skill.Files.Should().Contain(f => f.Path == "SKILL.md");
            skill.Files.Count.Should().BeLessThan(512);
            skill.Files.Sum(f => f.Bytes.Length).Should().BeLessThan(16 * 1024 * 1024);

            // A skill that tells the model to call a tool must name one that exists.
            foreach (var file in skill.Files)
                foreach (var word in ToolLikeWords(file.Text, backticked: true))
                    McpToolScopes.All.Should().ContainKey(word, $"{file.Uri} names the tool {word}");
        }
    }

    // ---- helpers ----

    private static readonly string[] ToolVerbs =
    [
        "get_", "list_", "search_", "log_", "create_", "add_", "delete_", "update_", "promote_", "toggle_", "save_",
        "send_", "respond_", "analyze_", "upload_", "remove_", "record_", "duplicate_",
    ];

    /// <summary>Words shaped like a tool name, so a made-up or renamed tool in a prompt or skill is caught.</summary>
    private static IEnumerable<string> ToolLikeWords(string text, bool backticked = false)
    {
        var pattern = backticked ? @"`([a-z]+(?:_[a-z]+)+)`" : @"\b([a-z]+(?:_[a-z]+)+)\b";
        return System.Text.RegularExpressions.Regex.Matches(text, pattern)
            .Select(m => m.Groups[1].Value)
            .Where(word => ToolVerbs.Any(word.StartsWith))
            .Distinct();
    }

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

    private HttpClient Authorized(params string[] scopes)
    {
        var token = "t-" + Guid.NewGuid().ToString("N");
        _backend.Setup(b => b.IntrospectAsync(token, It.IsAny<CancellationToken>())).ReturnsAsync(Introspection(scopes));
        return ClientWith(token);
    }

    private static async Task<JsonElement> RpcAsync(HttpClient client, string method, object? parameters = null)
    {
        var response = await client.PostMcpAsync(new { jsonrpc = "2.0", id = 1, method, @params = parameters });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static List<string> Names(JsonElement response, string collection, string field) =>
        response.GetProperty("result").GetProperty(collection).EnumerateArray().Select(e => e.GetProperty(field).GetString()!).ToList();
}
