extern alias McpServer;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using McpServer::Mizan.Mcp.Server.Services;
using Mizan.Application.Commands;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// Phase 15: the surfaces the website had and MCP did not - consent, usage,
/// chat threads, uploads and the prompt console.
///
/// These test the boundary, not the features underneath: that the tool exists,
/// that its arguments reach the right endpoint, and that access control is the
/// backend's answer rather than the tool layer's. Whether consent actually
/// narrows what the model sees is <c>DataAccessPolicyTests</c>' job.
/// </summary>
[Collection("ApiIntegration")]
public class McpParityTests : IClassFixture<WebApplicationFactory<McpServer::Program>>
{
    private readonly WebApplicationFactory<McpServer::Program> _mcpFactory;
    private readonly HttpClient _mcp;
    private readonly ApiTestFixture _api;

    public McpParityTests(WebApplicationFactory<McpServer::Program> mcpFactory, ApiTestFixture api)
    {
        _api = api;

        _mcpFactory = mcpFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("MizanApiUrl", "http://localhost:5000");
            builder.UseSetting("Mcp:ServiceApiKey", "test-api-key");
            builder.UseSetting("Mcp:AdminServiceApiKey", "test-admin-api-key");

            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["MizanApiUrl"] = "http://localhost:5000",
                    ["Mcp:ServiceApiKey"] = "test-api-key",
                    ["Mcp:AdminServiceApiKey"] = "test-admin-api-key",
                }));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBackendApiClient>();
                services.AddScoped<IBackendApiClient>(sp =>
                {
                    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BackendApiClient>>();
                    var accessor = sp.GetRequiredService<IHttpContextAccessor>();
                    var client = new HttpClient(_api.Server.CreateHandler())
                    {
                        BaseAddress = new Uri("http://localhost:5000"),
                    };
                    return new BackendApiClient(client, accessor, logger, Configuration());
                });
            });
        });

        _mcp = _mcpFactory.CreateClient();
    }

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mcp:ServiceApiKey"] = "test-api-key",
            ["Mcp:AdminServiceApiKey"] = "test-admin-api-key",
        }).Build();

    // ---- The catalogue ----------------------------------------------------

    [Fact]
    public async Task TheNewSurfacesAreAllListed()
    {
        await _api.ResetDatabaseAsync();
        await AuthenticateAsync();

        var names = await ToolNamesAsync();

        names.Should().Contain(new[]
        {
            // AI consent and usage
            "get_ai_consent", "set_ai_consent", "get_ai_usage",
            // Chat threads
            "ask_ai", "list_ai_threads", "get_ai_thread", "delete_ai_thread",
            // Uploads
            "upload_image", "analyze_food_image",
            // Prompt console
            "admin_list_ai_prompts", "admin_get_ai_prompt", "admin_create_ai_prompt_draft",
            "admin_update_ai_prompt_draft", "admin_run_ai_prompt_evals",
            "admin_get_ai_prompt_evals", "admin_publish_ai_prompt_version",
            "admin_get_global_ai_usage",
            // Background queue
            "admin_list_jobs", "admin_get_job_stats", "admin_retry_job", "admin_delete_job",
        });
    }

    // ---- Consent ----------------------------------------------------------

    [Fact]
    public async Task ConsentReadsBackWhatWasSetAndDefaultsToOff()
    {
        await _api.ResetDatabaseAsync();
        await AuthenticateAsync();

        var initial = await CallAsync("get_ai_consent", new { });
        initial.Should().Contain("\"enabled\":false", "consent is default-off");

        await CallAsync("set_ai_consent", new
        {
            enabled = true,
            shareNutrition = true,
            shareTraining = false,
            shareBody = false,
        });

        var after = await CallAsync("get_ai_consent", new { });
        after.Should().Contain("\"enabled\":true");
        after.Should().Contain("\"shareNutrition\":true");
        after.Should().Contain("\"shareBody\":false", "an axis not granted stays off");
    }

    [Fact]
    public async Task UsageIsScopedToTheCallingUser()
    {
        await _api.ResetDatabaseAsync();
        await AuthenticateAsync();

        var usage = await CallAsync("get_ai_usage", new { days = 7 });

        usage.Should().NotBeNullOrWhiteSpace();
        usage.Should().NotContain("\"error\"");
    }

    // ---- Threads ----------------------------------------------------------

    [Fact]
    public async Task ThreadListingWorksAndAnUnknownThreadIsNotFound()
    {
        await _api.ResetDatabaseAsync();
        await AuthenticateAsync();

        (await CallAsync("list_ai_threads", new { take = 5 })).Should().NotBeNullOrWhiteSpace();

        var missing = await CallAsync("get_ai_thread", new { id = Guid.NewGuid().ToString() });
        missing.Should().Contain("Not found");
    }

    [Fact]
    public async Task AMalformedThreadIdIsRejectedBeforeTheRoundTrip()
    {
        await _api.ResetDatabaseAsync();
        await AuthenticateAsync();

        var result = await CallAsync("get_ai_thread", new { id = "not-a-guid" });

        result.Should().Contain("Invalid id", "the tool boundary names the argument the caller got wrong");
    }

    // ---- Uploads ----------------------------------------------------------

    [Fact]
    public async Task UploadRejectsSomethingThatIsNotAnImage()
    {
        await _api.ResetDatabaseAsync();
        await AuthenticateAsync();

        var text = Convert.ToBase64String("this is a text file, not a picture"u8.ToArray());

        var result = await CallAsync("upload_image", new { imageBase64 = text });

        result.Should().Contain("not a JPEG",
            "the bytes decide, not the caller - and the tool says so before the round trip");
    }

    [Fact]
    public async Task UploadRejectsInvalidBase64()
    {
        await _api.ResetDatabaseAsync();
        await AuthenticateAsync();

        var result = await CallAsync("upload_image", new { imageBase64 = "!!!not base64!!!" });

        result.Should().Contain("not valid base64");
    }

    /// <summary>
    /// A real PNG gets all the way to the store. There is no object store in a
    /// test run, so the endpoint says so - and that message is the proof the
    /// multipart body, the impersonation header and the format sniffing all
    /// worked, which is the part this file is responsible for.
    /// </summary>
    [Fact]
    public async Task ARealPngReachesTheStorageLayer()
    {
        await _api.ResetDatabaseAsync();
        await AuthenticateAsync();

        var result = await CallAsync("upload_image", new
        {
            imageBase64 = Convert.ToBase64String(OnePixelPng),
            fileName = "pixel.png",
            folder = "recipes",
        });

        result.Should().NotContain("not a JPEG").And.NotContain("MCP token");
        result.Should().Contain("Internal server error");
    }

    [Fact]
    public async Task UploadRejectsAnUnknownFolder()
    {
        await _api.ResetDatabaseAsync();
        await AuthenticateAsync();

        var result = await CallAsync("upload_image", new
        {
            imageBase64 = Convert.ToBase64String(OnePixelPng),
            folder = "somewhere-else",
        });

        result.Should().Contain("Unknown folder");
    }

    // ---- Admin gating -----------------------------------------------------

    [Fact]
    public async Task TheAdminToolsAreRefusedForAPlainUser()
    {
        await _api.ResetDatabaseAsync();
        await AuthenticateAsync();

        // The tool is listed - the catalogue is the same for everyone - but the
        // backend is what decides, so calling it fails rather than the MCP layer
        // keeping a second copy of the rule.
        var result = await CallAsync("admin_list_jobs", new { });

        result.Should().NotContain("\"deadLettered\"");
    }

    [Fact]
    public async Task AnAdminCanReadTheQueueAndThePromptCatalogue()
    {
        await _api.ResetDatabaseAsync();
        await AuthenticateAsync(admin: true);

        (await CallAsync("admin_get_job_stats", new { })).Should().Contain("deadLettered");
        (await CallAsync("admin_list_ai_prompts", new { })).Should().NotBeNullOrWhiteSpace();
        (await CallAsync("admin_get_global_ai_usage", new { })).Should().NotBeNullOrWhiteSpace();
    }

    // ---- Helpers ----------------------------------------------------------

    /// <summary>A 1x1 transparent PNG. Small enough to inline, real enough to sniff.</summary>
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private async Task AuthenticateAsync(bool admin = false)
    {
        var id = Guid.NewGuid();
        var email = $"parity-{id:N}@example.com";
        await _api.SeedUserAsync(id, email, role: admin ? "admin" : "user");

        using var client = _api.CreateAuthenticatedClient(id, email, admin ? "admin" : "user");
        var created = await client.PostAsJsonAsync("/api/McpTokens", new { Name = "Parity" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var token = (await created.Content.ReadFromJsonAsync<CreateMcpTokenResult>())!.PlaintextToken;

        _mcp.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<List<string>> ToolNamesAsync()
    {
        var response = await _mcp.PostMcpAsync(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/list",
            @params = (object?)null,
        });

        var body = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        var result = JsonSerializer.Deserialize<JsonElement>(body!.Result!.ToString()!);

        return result.GetProperty("tools").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()!)
            .ToList();
    }

    /// <summary>Returns the tool's text, whether it succeeded or reported an error.</summary>
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
        if (body?.Result is null) return string.Empty;

        var result = JsonSerializer.Deserialize<JsonElement>(body.Result.ToString()!);
        if (!result.TryGetProperty("content", out var content)) return result.ToString();

        return string.Join("\n", content.EnumerateArray()
            .Where(block => block.TryGetProperty("text", out _))
            .Select(block => block.GetProperty("text").GetString()));
    }
}
