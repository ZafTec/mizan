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
        await AuthenticateAsync(admin: true);

        var names = await ToolNamesAsync();

        names.Should().Contain(new[]
        {
            // AI consent and usage
            "get_ai_consent", "set_ai_consent", "get_ai_usage",
            // Chat threads
            "ask_ai", "list_ai_threads", "get_ai_thread", "delete_ai_thread", "get_ai_conversation",
            // Uploads
            "upload_image", "analyze_food_image",
            // Prompt console
            "admin_list_ai_prompts", "admin_get_ai_prompt", "admin_create_ai_prompt_draft",
            "admin_update_ai_prompt_draft", "admin_run_ai_prompt_evals",
            "admin_get_ai_prompt_evals", "admin_publish_ai_prompt_version",
            "admin_get_global_ai_usage",
            // Feature flags, allowance overrides and platform usage
            "admin_list_feature_flags", "admin_set_feature_flag", "admin_set_feature_flag_override",
            "admin_clear_feature_flag_override", "admin_list_usage_limits", "admin_set_usage_limit",
            "admin_clear_usage_limit", "admin_get_platform_usage",
            // Background queue
            "admin_list_jobs", "admin_get_job_stats", "admin_retry_job", "admin_delete_job",
            // Runtime settings
            "admin_list_settings", "admin_set_setting", "admin_clear_setting", "admin_test_smtp",
        });
    }

    [Fact]
    public async Task AdminToolsAreNotEvenListedForAPlainUser()
    {
        await _api.ResetDatabaseAsync();
        await AuthenticateAsync();

        (await ToolNamesAsync()).Should().NotContain(name => name.StartsWith("admin_"));
    }

    // ---- Planning ---------------------------------------------------------

    [Fact]
    public async Task GenerateMealPlanDraftsThroughTheApi_AndTheDraftIsListed()
    {
        await _api.ResetDatabaseAsync();
        _api.Ai.Reset();
        await AuthenticateAsync();
        await _api.SeedFoodAsync("Eggs", 143, 12.6m, 0.7m, 9.5m);
        _api.Ai.Reply("""
            {"name":"Connected week","note":null,"prepSessions":[],"days":[{"dayIndex":0,"entries":[
              {"slot":"BREAKFAST","kind":"food","foodName":"Eggs","recipeId":null,"grams":120,"servings":null,"note":null,"prepSessionIndex":null},
              {"slot":"LUNCH","kind":"text","foodName":"Market salad","recipeId":null,"grams":null,"servings":null,"note":null,"prepSessionIndex":null}]}]}
            """);

        var generated = await CallAsync("generate_meal_plan", new { startDate = "2026-11-02", durationDays = 1, preference = "salad" });

        generated.Should().Contain("\"status\":\"Draft\"").And.Contain("Connected week").And.Contain("Market salad");
        (await CallAsync("list_meal_plans", new { status = "Draft" })).Should().Contain("Connected week");
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
        (await CallAsync("get_ai_conversation", new { })).Should().Contain("threadId");

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

    /// <summary>
    /// The address is passed on, not the bytes: the API downloads the photo and analyzes it, and upload_image
    /// gets as far as the store the same way.
    /// </summary>
    [Fact]
    public async Task APhotoByAddress_IsAnalyzedAndStored_WithoutItsBytesInTheCall()
    {
        await _api.ResetDatabaseAsync();
        _api.Ai.Reset();
        var userId = await AuthenticateAsync();
        await _api.GrantProAsync(userId);
        var url = $"https://images.example.com/{Guid.NewGuid():N}.png";
        _api.RemoteImages.Serve(url, OnePixelPng);
        _api.Ai.Reply("""
            {"foods":[{"name":"Shiro","portionGrams":200,"calories":300,"protein":14,"carbs":30,"fat":12}],
             "totalCalories":300,"confidence":0.8,"note":null}
            """);

        var analysis = await CallAsync("analyze_food_image", new { imageUrl = url });
        analysis.Should().Contain("Shiro");

        var stored = await CallAsync("upload_image", new { imageUrl = url, folder = "recipes" });
        stored.Should().NotContain("not a JPEG").And.Contain("Internal server error", "there is no object store in a test run");
        _api.RemoteImages.Requested.Count(r => r == url).Should().Be(2);
    }

    [Fact]
    public async Task AnImageToolTakesExactlyOneOfAnAddressOrBytes()
    {
        await _api.ResetDatabaseAsync();
        await AuthenticateAsync();

        (await CallAsync("upload_image", new { })).Should().Contain("imageUrl (preferred) or imageBase64");
        (await CallAsync("analyze_food_image", new { imageUrl = "https://images.example.com/a.png", imageBase64 = Convert.ToBase64String(OnePixelPng) }))
            .Should().Contain("not both");
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

        // The tool is not listed for a plain user, and calling it anyway is refused
        // before it reaches the API. The API refuses it too, as a second line.
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

    // ---- Runtime settings -------------------------------------------------

    [Fact]
    public async Task AnAdminSetsAndClearsASettingThroughMcp_AndASecretStaysHidden()
    {
        await _api.ResetDatabaseAsync();
        await AuthenticateAsync(admin: true);

        (await CallAsync("admin_set_setting", new { key = "Smtp.Host", value = "smtp.example.test" }))
            .Should().Contain("\"source\":\"database\"").And.Contain("smtp.example.test");
        (await CallAsync("admin_set_setting", new { key = "Smtp.Password", value = "mcp-secret-value" }))
            .Should().NotContain("mcp-secret-value").And.Contain("\"hasValue\":true");
        (await CallAsync("admin_set_setting", new { key = "Smtp.Port", value = "99999" }))
            .Should().Contain("65535", "the catalog's message comes back to the caller");

        var listing = await CallAsync("admin_list_settings", new { });
        listing.Should().Contain("Smtp.Host").And.NotContain("mcp-secret-value");

        (await CallAsync("admin_clear_setting", new { key = "Smtp.Host" }))
            .Should().Contain("\"source\":\"unset\"", "the test configuration has no SMTP host to go back to");
        (await CallAsync("admin_test_smtp", new { })).Should().Contain("jobId");
    }

    [Fact]
    public async Task AnAdminWorksTheMailLogContactInboxHealthAndSupportNotesFromMcp()
    {
        await _api.ResetDatabaseAsync();
        await AuthenticateAsync(admin: true);
        var subject = Guid.NewGuid();
        await _api.SeedUserAsync(subject, $"subject-{subject:N}@example.com");
        using (var anonymous = _api.CreateClient())
        {
            (await anonymous.PostAsJsonAsync("/api/Contact", new { name = "Ann", email = "ann@example.com", subject = "Hello", message = "From the form" }))
                .EnsureSuccessStatusCode();
        }

        (await ToolNamesAsync()).Should().Contain(new[]
        {
            "admin_list_mail", "admin_retry_mail", "admin_send_announcement", "admin_list_contact",
            "admin_reply_contact", "admin_mark_contact_handled", "admin_get_system_health",
            "admin_list_support_notes", "admin_add_support_note",
        });

        (await CallAsync("admin_get_system_health", new { })).Should().Contain("\"name\":\"database\"");

        var inbox = await CallAsync("admin_list_contact", new { status = "New" });
        inbox.Should().Contain("From the form");
        var contactId = System.Text.RegularExpressions.Regex.Match(inbox, "\"id\":\"([0-9a-f-]{36})\"").Groups[1].Value;
        (await CallAsync("admin_reply_contact", new { id = contactId, body = "Thanks for writing." }))
            .Should().Contain("\"status\":\"Handled\"");

        (await CallAsync("admin_list_mail", new { kind = "contact.reply" })).Should().Contain("ann@example.com");

        (await CallAsync("admin_send_announcement", new { subject = "Hi", body = "One person.", audience = $"user:{subject}" }))
            .Should().Contain("\"queued\":1");

        (await CallAsync("admin_add_support_note", new { subjectType = "User", subjectId = subject.ToString(), body = "Called about billing." }))
            .Should().Contain("Called about billing.");
        (await CallAsync("admin_list_support_notes", new { subjectType = "User", subjectId = subject.ToString() }))
            .Should().Contain("Called about billing.");
    }

    [Fact]
    public async Task AnAdminSwitchesAFeatureOffForAHousehold_CapsAUser_AndReadsUsage_ThroughTools()
    {
        await _api.ResetDatabaseAsync();
        await AuthenticateAsync(admin: true);
        var world = new PlatformAdminWorld(_api);
        var (person, _) = await world.PersonAsync("member");
        var home = await world.HouseholdAsync("Home", person);

        (await CallAsync("admin_set_feature_flag_override", new { key = "social.feed", householdId = home.ToString(), enabled = false }))
            .Should().Contain("\"enabled\":false");
        (await CallAsync("admin_list_feature_flags", new { })).Should().Contain("\"disabledOverrides\":1");
        (await CallAsync("admin_set_usage_limit", new { scope = "User", subjectId = person.ToString(), line = "Personal", dailyRequests = 2 }))
            .Should().Contain("\"source\":\"User\"");
        (await CallAsync("admin_list_usage_limits", new { scope = "User" })).Should().Contain(person.ToString());
        (await CallAsync("admin_get_platform_usage", new { groupBy = "feature" })).Should().Contain("\"groupBy\":\"feature\"");
        (await CallAsync("admin_clear_usage_limit", new { scope = "User", subjectId = person.ToString(), line = "Personal" }))
            .Should().NotContain("error");
        (await CallAsync("admin_clear_feature_flag_override", new { key = "social.feed", householdId = home.ToString() }))
            .Should().NotContain("error");
        (await CallAsync("admin_set_feature_flag", new { key = "social.feed", enabled = true })).Should().Contain("\"defaultEnabled\":true");
    }

    // ---- Helpers ----------------------------------------------------------

    /// <summary>A 1x1 transparent PNG. Small enough to inline, real enough to sniff.</summary>
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private async Task<Guid> AuthenticateAsync(bool admin = false)
    {
        var id = Guid.NewGuid();
        var email = $"parity-{id:N}@example.com";
        await _api.SeedUserAsync(id, email, role: admin ? "admin" : "user");

        var token = (await _api.CreateMcpAccessAsync(id)).Token;

        _mcp.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return id;
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
