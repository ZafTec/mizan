extern alias McpServer;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using McpServer::Mizan.Mcp.Server.Authentication;
using McpServer::Mizan.Mcp.Server.Services;
using NSec.Cryptography;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// A hosted assistant reaching Mizan through the gateway (docs/MCP.md#served-to-a-hosted-assistant-through-the-gateway).
/// The gateway presents its key, and each tool call carries a token naming the person, signed with an Ed25519 key
/// that a stub serves as the gateway's JWKS. The values are neutral samples.
/// </summary>
[Collection("ApiIntegration")]
public class McpGatewayTests : IClassFixture<WebApplicationFactory<McpServer::Program>>, IAsyncLifetime
{
    private const string ClientKey = "sample-gateway-client-key-0123456789abcdef";
    private const string MetaKey = "example/end_user";
    private const string Issuer = "https://gateway.test";
    private const string Audience = "mizan-mcp-sample";
    private const string TokenType = "example-identity+jwt";
    private const string EndUserIssuer = "https://mizan.test";
    private const string KeyId = "sample-key-1";

    private static readonly Key SigningKey = Key.Create(SignatureAlgorithm.Ed25519);
    private static readonly Key StrangerKey = Key.Create(SignatureAlgorithm.Ed25519);

    private readonly ApiTestFixture _apiFixture;
    private readonly WebApplicationFactory<McpServer::Program> _factory;

    public McpGatewayTests(WebApplicationFactory<McpServer::Program> mcpFactory, ApiTestFixture apiFixture)
    {
        _apiFixture = apiFixture;
        _factory = mcpFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("MizanApiUrl", "http://localhost:5000");
            builder.UseSetting("ServiceApiKey", "test-api-key");
            builder.UseSetting("Mcp:ServiceApiKey", "test-api-key");
            builder.UseSetting("Mcp:AdminServiceApiKey", "test-admin-api-key");
            builder.UseSetting("Mcp:Toolset", "tasks");
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient(GatewayKeys.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => new JwksStub());
                services.RemoveAll<IBackendApiClient>();
                services.AddScoped<IBackendApiClient>(sp =>
                {
                    var client = new HttpClient(_apiFixture.Server.CreateHandler()) { BaseAddress = new Uri("http://localhost:5000") };
                    return new BackendApiClient(client, sp.GetRequiredService<IHttpContextAccessor>(),
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BackendApiClient>>());
                });
            });
        });
    }

    /// <summary>The gateway settings live in the API's admin settings; the MCP server reads them from there.</summary>
    public async Task InitializeAsync()
    {
        await _apiFixture.ResetDatabaseAsync();
        using (var scope = _apiFixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRuntimeSettings>().SetManyAsync(new Dictionary<string, string>
            {
                ["Gateway.ClientKey"] = ClientKey,
                ["Gateway.MetaKey"] = MetaKey,
                ["Gateway.JwksUrl"] = Issuer + "/.well-known/jwks.json",
                ["Gateway.Issuer"] = Issuer,
                ["Gateway.Audience"] = Audience,
                ["Gateway.TokenType"] = TokenType,
                ["EndUser.Issuer"] = EndUserIssuer,
            }, null, CancellationToken.None);
        }

        var settings = _factory.Services.GetRequiredService<GatewaySettings>();
        (await settings.RefreshAsync(CancellationToken.None)).Should().BeTrue();
        settings.Current.IsEnabled.Should().BeTrue();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_hosted_assistant_reads_todays_nutrition_as_the_person_its_token_names()
    {
        var userId = await UserAsync("user");
        await _apiFixture.GrantAiConsentAsync(userId, read: true, write: false);

        var listed = await SendAsync(Gateway(), "tools/list", new { });
        var names = listed.Result!.Value.GetProperty("tools").EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();
        names.Should().Contain(["get_nutrition", "log_food", "delete"]);

        // Every tool says what it takes, what it answers and its worst effect, so the host can ask before a destructive call.
        foreach (var tool in listed.Result!.Value.GetProperty("tools").EnumerateArray())
        {
            tool.GetProperty("inputSchema").GetProperty("type").GetString().Should().Be("object");
            tool.GetProperty("outputSchema").GetProperty("type").GetString().Should().Be("object");
            var hints = tool.GetProperty("annotations");
            foreach (var hint in new[] { "readOnlyHint", "destructiveHint", "idempotentHint", "openWorldHint" })
                hints.TryGetProperty(hint, out _).Should().BeTrue($"{tool.GetProperty("name")} carries {hint}");
        }

        var called = await CallAsync(Token(userId), "get_nutrition", new { view = "day", date = "2026-11-07" });
        called.Error.Should().BeNull(called.Error?.Message);
        var result = called.Result!.Value;
        result.TryGetProperty("isError", out var isError).Should().BeTrue();
        isError.GetBoolean().Should().BeFalse(result.ToString());
        result.GetProperty("structuredContent").GetProperty("date").GetString().Should().StartWith("2026-11-07");

        // The usage row has no connection and says which path the call came by; the audit row says the same.
        var logs = await _apiFixture.GetMcpUsageLogsByUserId(userId);
        logs.Should().ContainSingle(l => l.ToolName == "get_nutrition" && l.Success && l.GrantId == null && l.Parameters!.Contains("gateway"));
        using var scope = _apiFixture.Services.CreateScope();
        var audit = await scope.ServiceProvider.GetRequiredService<MizanDbContext>().AuditLogs.AsNoTracking()
            .Where(a => a.UserId == userId && a.Action == "CallTaskToolCommand").ToListAsync();
        audit.Should().ContainSingle().Which.Details.Should().Contain("\"Path\":\"gateway\"");
    }

    [Theory]
    [InlineData("missing", GatewayRefusals.Missing)]
    [InlineData("expired", GatewayRefusals.Expired)]
    [InlineData("audience", GatewayRefusals.Audience)]
    [InlineData("unknown_key", GatewayRefusals.UnknownKey)]
    [InlineData("reused", GatewayRefusals.Replayed)]
    [InlineData("end_user_issuer", GatewayRefusals.EndUserIssuer)]
    [InlineData("anonymous", GatewayRefusals.Anonymous)]
    [InlineData("unknown_subject", GatewayRefusals.SubjectUnknown)]
    [InlineData("wrong_client_key", null)]
    public async Task A_gateway_call_whose_token_does_not_hold_is_refused_with_a_code_and_never_runs(string fault, string? code)
    {
        var userId = await UserAsync("user");
        await _apiFixture.GrantAiConsentAsync(userId);
        var now = DateTimeOffset.UtcNow;

        string? token = fault switch
        {
            "missing" => null,
            "expired" => Token(userId, expires: now.AddMinutes(-5)),
            "audience" => Token(userId, audience: "another-service"),
            "unknown_key" => Token(userId, key: StrangerKey, kid: "not-published"),
            "end_user_issuer" => Token(userId, endUserIssuer: "https://elsewhere.test"),
            "anonymous" => Token(userId, anonymous: true),
            "unknown_subject" => Token(Guid.NewGuid()),
            _ => Token(userId),
        };

        if (fault == "wrong_client_key")
        {
            var refused = await Gateway("not-the-gateway-key").PostMcpAsync(Rpc("tools/call", Params("get_nutrition", new { view = "day" }, token)));
            refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            return;
        }

        if (fault == "reused")
            (await CallAsync(token, "get_nutrition", new { view = "day" })).Error.Should().BeNull();

        var response = await CallAsync(token, "get_nutrition", new { view = "day" });
        response.Error.Should().NotBeNull(response.Result?.ToString());
        response.Error!.Message.Should().StartWith(code + ":");
        if (token is not null) response.Error.Message.Should().NotContain(token.Split('.')[1]);

        // A refused token reaches nothing: no usage beyond the one good call of the reused case.
        (await _apiFixture.GetMcpUsageLogsByUserId(userId)).Should().HaveCount(fault == "reused" ? 1 : 0);
    }

    [Fact]
    public async Task A_hosted_assistant_is_held_to_the_assistant_consent_the_person_gave()
    {
        var userId = await UserAsync("user");

        var unshared = await ResultAsync(Token(userId), "get_nutrition", new { view = "day" });
        unshared.GetProperty("isError").GetBoolean().Should().BeTrue();
        var refusal = unshared.GetProperty("structuredContent");
        refusal.GetProperty("errorCode").GetString().Should().Be("ai_consent_required");
        refusal.GetProperty("axis").GetString().Should().Be("nutrition");
        refusal.GetProperty("access").GetString().Should().Be("read");
        unshared.GetProperty("content")[0].GetProperty("text").GetString().Should().Contain("Settings, Assistant");

        await _apiFixture.GrantAiConsentAsync(userId, read: true, write: false);
        (await ResultAsync(Token(userId), "get_nutrition", new { view = "day" })).GetProperty("isError").GetBoolean().Should().BeFalse();

        var write = await ResultAsync(Token(userId), "log_body", new { weightKg = 80 });
        write.GetProperty("isError").GetBoolean().Should().BeTrue(write.ToString());
        write.GetProperty("structuredContent").GetProperty("errorCode").GetString().Should().Be("ai_consent_required");
        write.GetProperty("structuredContent").GetProperty("access").GetString().Should().Be("write");
        using var scope = _apiFixture.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<MizanDbContext>().BodyMeasurements.CountAsync(m => m.UserId == userId)).Should().Be(0);
    }

    [Fact]
    public async Task A_trainer_through_the_gateway_reads_only_what_the_client_shared_and_allowed_ai()
    {
        var trainer = await UserAsync("trainer");
        var client = await UserAsync("user");
        using (var scope = _apiFixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
            db.TrainerClientRelationships.Add(new TrainerClientRelationship
            {
                Id = Guid.CreateVersion7(),
                TrainerId = trainer,
                ClientId = client,
                Status = "active",
                CanViewNutrition = true,
                CanViewWorkouts = false,
                CanViewMeasurements = false,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        await _apiFixture.GrantAiConsentAsync(trainer);

        // The client shared nutrition with the coach but has not let AI read it.
        var withoutConsent = await ResultAsync(Token(trainer), "get_clients", new { view = "nutrition", clientId = client });
        withoutConsent.GetProperty("structuredContent").GetProperty("errorCode").GetString().Should().Be("client_ai_consent_required");

        await _apiFixture.GrantAiConsentAsync(client);
        (await ResultAsync(Token(trainer), "get_clients", new { view = "nutrition", clientId = client }))
            .GetProperty("isError").GetBoolean().Should().BeFalse();

        var outsideGrant = await ResultAsync(Token(trainer), "get_clients", new { view = "workouts", clientId = client });
        outsideGrant.GetProperty("isError").GetBoolean().Should().BeTrue();
        outsideGrant.GetProperty("structuredContent").GetProperty("errorCode").GetString().Should().Be("forbidden");
    }

    [Fact]
    public async Task Admin_tools_are_neither_listed_nor_served_through_the_gateway()
    {
        var admin = await UserAsync("admin");
        await _apiFixture.GrantAiConsentAsync(admin);

        var listed = await SendAsync(Gateway(), "tools/list", new { });
        listed.Result!.Value.GetProperty("tools").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()).Should().NotContain(name => name!.StartsWith("admin_") || name == "find_action");

        var called = await ResultAsync(Token(admin), "admin_overview", new { });
        called.GetProperty("isError").GetBoolean().Should().BeTrue(called.ToString());
        called.GetProperty("structuredContent").GetProperty("needs").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_connected_app_is_not_held_to_the_assistant_consent_switches()
    {
        var userId = await UserAsync("user");
        var access = await _apiFixture.CreateMcpAccessAsync(userId, ["nutrition:read"]);
        var app = _factory.CreateClient();
        app.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access.Token);

        var response = await app.PostMcpAsync(Rpc("tools/call", Params("get_nutrition", new { view = "day" }, token: null)));
        var body = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        body!.Error.Should().BeNull(body.Error?.Message);
        body.Result!.Value.GetProperty("isError").GetBoolean().Should().BeFalse(body.Result.Value.ToString());
    }

    /// <summary>
    /// A hosted assistant's host caps a tool reply, so the largest pages a busy account can ask for stay well under 32 KB:
    /// a month's plan, a full page of foods, of workouts with every set, and of measurements.
    /// </summary>
    [Fact]
    public async Task A_busy_accounts_largest_replies_stay_within_the_reply_budget()
    {
        const int Budget = 24 * 1024;
        var world = new PlanningWorld(_apiFixture);
        var (userId, web) = await world.PersonAsync("busy");
        await _apiFixture.GrantAiConsentAsync(userId);

        var foods = new List<Food>();
        for (var i = 0; i < 100; i++) foods.Add(await world.FoodAsync($"Budget food {i:000} with a longer catalogue name", 120 + i, 10, 15, 4));
        var recipes = new List<Recipe>();
        for (var r = 0; r < 6; r++)
            recipes.Add(await world.RecipeAsync(userId, $"Budget recipe {r} with several components", 4, false,
                foods.Skip(r * 8).Take(8).Select(f => ((Food?)f, (decimal?)150m, (string?)"g", (string?)null)).ToArray()));

        var entries = new List<Mizan.Contracts.Planning.PlanEntryRequest>();
        for (var day = 0; day < 31; day++)
        {
            entries.Add(PlanningWorld.FoodEntry(foods[day].Id, 80, day, "BREAKFAST"));
            entries.Add(PlanningWorld.FoodEntry(foods[day + 31].Id, 200, day, "LUNCH"));
            entries.Add(PlanningWorld.RecipeEntry(recipes[day % recipes.Count].Id, 1, day, "DINNER"));
            entries.Add(PlanningWorld.TextEntry("A piece of fruit and a handful of nuts", day));
        }
        var planId = await PlanningWorld.CreatePlanAsync(web, new Mizan.Contracts.Planning.CreateMealPlanRequest
        {
            Name = "A month of meals",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DurationDays = 31,
            Entries = entries,
        });

        await world.DbAsync(async db =>
        {
            var exercises = Enumerable.Range(0, 6).Select(i => new Exercise
            {
                Id = Guid.NewGuid(),
                Name = $"Budget lift {i}",
                Category = "strength",
                MuscleGroup = "legs",
                IsApproved = true,
                CreatedAt = DateTime.UtcNow,
            }).ToList();
            db.Exercises.AddRange(exercises);
            for (var w = 0; w < 100; w++)
            {
                var workout = new Workout
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Name = $"Session {w}",
                    WorkoutDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-w),
                    DurationMinutes = 60,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                for (var e = 0; e < exercises.Count; e++)
                {
                    var done = new WorkoutExercise { Id = Guid.NewGuid(), WorkoutId = workout.Id, ExerciseId = exercises[e].Id, SortOrder = e };
                    for (var s = 1; s <= 4; s++)
                        done.Sets.Add(new ExerciseSet { Id = Guid.NewGuid(), WorkoutExerciseId = done.Id, SetNumber = s, Reps = 8, WeightKg = 60 + s * 2.5m, Completed = true });
                    workout.Exercises.Add(done);
                }
                db.Workouts.Add(workout);
                db.BodyMeasurements.Add(new BodyMeasurement
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    MeasurementDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-w),
                    WeightKg = 80 - w * 0.05m,
                    BodyFatPercentage = 18,
                    WaistCm = 84,
                    Notes = "Morning, before breakfast",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
            return await db.SaveChangesAsync();
        });

        var token = () => Token(userId);
        var calls = new (string Tool, object Arguments)[]
        {
            ("get_meal_plan", new { view = "plan", planId }),
            ("get_meal_plan", new { view = "day", planId }),
            ("find_food", new { kind = "food", query = "Budget food", pageSize = 100 }),
            ("find_food", new { kind = "recipe", recipeId = recipes[0].Id }),
            ("get_training", new { view = "history", pageSize = 100 }),
            ("get_body_trend", new { pageSize = 100 }),
            ("get_nutrition", new { view = "range", from = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-89).ToString("yyyy-MM-dd") }),
        };

        var sizes = new List<(int Bytes, string Line)>();
        foreach (var (tool, arguments) in calls)
        {
            var reply = await ResultAsync(token(), tool, arguments);
            reply.GetProperty("isError").GetBoolean().Should().BeFalse($"{tool} {JsonSerializer.Serialize(arguments)}: {reply}");
            var bytes = Encoding.UTF8.GetByteCount(reply.GetRawText());
            var text = Encoding.UTF8.GetByteCount(reply.GetProperty("content")[0].GetProperty("text").GetString()!);
            sizes.Add((bytes, $"{tool} {JsonSerializer.Serialize(arguments)}: {bytes} bytes ({text} text)"));
        }

        var report = string.Join(Environment.NewLine, sizes.Select(s => s.Line));
        Console.WriteLine(report);
        sizes.Should().OnlyContain(s => s.Bytes < Budget, report);
    }

    private async Task<Guid> UserAsync(string role)
    {
        var id = Guid.NewGuid();
        await _apiFixture.SeedUserAsync(id, $"{role}-{id:N}@example.com", role: role);
        return id;
    }

    private HttpClient Gateway(string key = ClientKey)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return client;
    }

    private async Task<JsonRpcResponse> CallAsync(string? token, string tool, object arguments) =>
        await SendAsync(Gateway(), "tools/call", Params(tool, arguments, token));

    private async Task<JsonElement> ResultAsync(string token, string tool, object arguments)
    {
        var response = await CallAsync(token, tool, arguments);
        response.Error.Should().BeNull(response.Error?.Message);
        return response.Result!.Value;
    }

    private static async Task<JsonRpcResponse> SendAsync(HttpClient client, string method, object parameters)
    {
        var response = await client.PostMcpAsync(Rpc(method, parameters));
        return (await response.Content.ReadFromJsonAsync<JsonRpcResponse>())!;
    }

    private static object Rpc(string method, object parameters) =>
        new { jsonrpc = "2.0", id = Guid.NewGuid().ToString("N"), method, @params = parameters };

    private static Dictionary<string, object?> Params(string tool, object arguments, string? token)
    {
        var parameters = new Dictionary<string, object?> { ["name"] = tool, ["arguments"] = arguments };
        if (token is not null) parameters["_meta"] = new Dictionary<string, object> { [MetaKey] = token };
        return parameters;
    }

    /// <summary>A token as the gateway signs it. Each argument spoils one check.</summary>
    private static string Token(
        Guid subject, DateTimeOffset? expires = null, string audience = Audience, Key? key = null, string kid = KeyId,
        string endUserIssuer = EndUserIssuer, bool anonymous = false)
    {
        var now = DateTimeOffset.UtcNow;
        var header = new Dictionary<string, object> { ["alg"] = "EdDSA", ["typ"] = TokenType, ["kid"] = kid };
        var claims = new Dictionary<string, object>
        {
            ["iss"] = Issuer,
            ["aud"] = audience,
            ["sub"] = subject.ToString(),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = (expires ?? now.AddMinutes(5)).ToUnixTimeSeconds(),
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["anonymous"] = anonymous,
            ["end_user_issuer"] = endUserIssuer,
        };
        var signingInput = Encode(JsonSerializer.SerializeToUtf8Bytes(header)) + "." + Encode(JsonSerializer.SerializeToUtf8Bytes(claims));
        var signature = SignatureAlgorithm.Ed25519.Sign(key ?? SigningKey, Encoding.ASCII.GetBytes(signingInput));
        return signingInput + "." + Encode(signature);
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>The gateway's JWKS: the one sample key.</summary>
    private sealed class JwksStub : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var jwks = new
            {
                keys = new[]
                {
                    new { kty = "OKP", crv = "Ed25519", use = "sig", alg = "EdDSA", kid = KeyId, x = Encode(SigningKey.PublicKey.Export(KeyBlobFormat.RawPublicKey)) },
                },
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(jwks) });
        }
    }
}
