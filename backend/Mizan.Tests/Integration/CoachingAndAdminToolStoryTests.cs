using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Ai;
using Mizan.Contracts.Mcp;
using Mizan.Domain.Entities;
using Mizan.Domain.Identity;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The coaching and admin task tools through the API (docs/MCP.md#task-tools): the trainer and
/// admin ceilings, the double consent on a client's data, the coach's allowance, the admin
/// action runner with its preview and confirm step, and the safety stops.
/// </summary>
[Collection("ApiIntegration")]
public class CoachingAndAdminToolStoryTests
{
    private readonly ApiTestFixture _fixture;

    public CoachingAndAdminToolStoryTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ACoach_IsOfferedTheCoachingTools_AndAnAppReadsAClient_OnlyWhenTheClientSharedItAndAllowedAi()
    {
        await _fixture.ResetDatabaseAsync();
        var (coach, client) = await PairAsync(nutrition: true);
        using var browser = _fixture.CreateAuthenticatedClient(coach, "coach@example.com", "trainer");
        var manifest = await ManifestAsync(browser);
        manifest.Should().HaveCount(31).And.Contain(["get_clients", "manage_clients", "draft_client_plan", "save_plan_template", "ask_about_client"]);
        manifest.Should().NotContain(name => name.StartsWith("admin_"));

        // The connection holds the coaching tools and, like any read of nutrition, the nutrition scope.
        using var app = AppClient(coach, await GrantAsync(coach, [McpScopes.CoachingRead, McpScopes.NutritionRead]));

        var withoutConsent = await CallAsync(app, "get_clients", new { view = "nutrition", clientId = client });
        withoutConsent.GetProperty("errorCode").GetString().Should().Be("client_ai_consent_required", withoutConsent.ToString());

        await ConsentAsync(client, nutrition: true);
        var withConsent = await CallAsync(app, "get_clients", new { view = "nutrition", clientId = client });
        withConsent.GetProperty("isError").GetBoolean().Should().BeFalse(withConsent.ToString());

        // Consent to AI does not open what the client never shared with the coach.
        var notShared = await CallAsync(app, "get_clients", new { view = "workouts", clientId = client });
        notShared.GetProperty("errorCode").GetString().Should().Be("forbidden");

        // A person who is not a trainer is not offered the coaching tools at all.
        (await CallAsync(AppClient(client, await GrantAsync(client, [McpScopes.CoachingRead])), "get_clients", new { }))
            .GetProperty("needs").GetString().Should().Be("role:trainer");
    }

    [Fact]
    public async Task AskingAboutAClient_IsChargedToTheCoachsAllowance()
    {
        await _fixture.ResetDatabaseAsync();
        var (coach, client) = await PairAsync(nutrition: true);
        await ConsentAsync(client, nutrition: true);
        _fixture.Ai.Reply("Their protein is steady.");
        using var app = AppClient(coach, await GrantAsync(coach, [McpScopes.CoachingWrite]));

        var answer = await CallAsync(app, "ask_about_client", new { clientId = client, message = "How is their protein?" });

        answer.GetProperty("isError").GetBoolean().Should().BeFalse(answer.ToString());
        answer.GetProperty("data").ToString().Should().Contain("Their protein is steady.");
        await WithDbAsync(async db =>
        {
            (await db.AiUsageLogs.Where(l => l.UserId == coach).ToListAsync()).Should().ContainSingle(l => l.Feature == AiFeatures.TrainerClient);
            (await db.AiUsageLogs.AnyAsync(l => l.UserId == client)).Should().BeFalse();
        });
    }

    [Fact]
    public async Task AnAdmin_IsOfferedTheAdminTools_ButNotWhileViewingMizanAsSomeoneElse()
    {
        await _fixture.ResetDatabaseAsync();
        var admin = await UserAsync("admin");
        using var browser = _fixture.CreateAuthenticatedClient(admin, "admin@example.com", "admin");
        var manifest = await ManifestAsync(browser);
        manifest.Should().HaveCount(32).And.Contain(["admin_overview", "admin_lookup", "admin_inbox", "find_action", "run_action", "confirm_action"]);
        manifest.Should().NotContain("get_clients");
        (await CallAsync(browser, "admin_overview", new { })).GetProperty("isError").GetBoolean().Should().BeFalse();

        var user = await UserAsync("user");
        using var viewingAs = await ImpersonatingAsync(admin, user);
        (await ManifestAsync(viewingAs)).Should().HaveCount(26).And.NotContain(name => name.StartsWith("admin_"));
        var refused = await CallAsync(viewingAs, "admin_overview", new { });
        refused.GetProperty("errorCode").GetString().Should().Be("not_allowed");
        refused.GetProperty("needs").GetString().Should().Be("not_impersonating");
    }

    [Fact]
    public async Task DeletingAHousehold_ShowsWhatGoes_AndRunsOnceThroughConfirmAction()
    {
        await _fixture.ResetDatabaseAsync();
        var admin = await UserAsync("admin");
        var owner = await UserAsync("user");
        var household = Guid.NewGuid();
        await WithDbAsync(async db =>
        {
            db.Households.Add(new Household { Id = household, Name = "Harbour Flat", CreatedBy = owner, CreatedAt = DateTime.UtcNow });
            db.HouseholdMembers.Add(new HouseholdMember { HouseholdId = household, UserId = owner, Role = "owner", JoinedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });
        using var app = AppClient(admin, await GrantAsync(admin, [McpScopes.AdminWrite], allowAdmin: true));

        var preview = await CallAsync(app, "run_action", new { action = "admin_delete_household", arguments = new { householdId = household } });
        preview.GetProperty("isError").GetBoolean().Should().BeFalse(preview.ToString());
        var facts = preview.GetProperty("confirmation").GetProperty("preview");
        facts.GetProperty("deletes").GetString().Should().Be("Harbour Flat");
        facts.GetProperty("members").EnumerateArray().Should().ContainSingle();
        preview.GetProperty("text").GetString().Should().Contain("confirm_action");
        (await HouseholdExistsAsync(household)).Should().BeTrue("a preview deletes nothing");
        var token = preview.GetProperty("confirmation").GetProperty("token").GetString()!;

        var done = await CallAsync(app, "confirm_action", new { token });
        done.GetProperty("isError").GetBoolean().Should().BeFalse(done.ToString());
        (await HouseholdExistsAsync(household)).Should().BeFalse();

        (await CallAsync(app, "confirm_action", new { token })).GetProperty("errorCode").GetString().Should().Be("token_used");

        // A connection that may only read cannot confirm.
        using var reader = AppClient(admin, await GrantAsync(admin, [McpScopes.AdminRead], allowAdmin: true));
        (await CallAsync(reader, "confirm_action", new { token })).GetProperty("needs").GetString().Should().Be(McpScopes.AdminWrite);
    }

    [Fact]
    public async Task RevokingAUsersSessions_IsASafetyStop_ThatRunsAtOnceAndIsAudited()
    {
        await _fixture.ResetDatabaseAsync();
        var admin = await UserAsync("admin");
        var user = await UserAsync("user");
        await _fixture.CreateSessionAsync(user);
        await _fixture.CreateSessionAsync(user);
        using var browser = _fixture.CreateAuthenticatedClient(admin, "admin@example.com", "admin");

        var stopped = await CallAsync(browser, "run_action", new { action = "admin_revoke_user_sessions", arguments = new { userId = user } });

        stopped.GetProperty("isError").GetBoolean().Should().BeFalse(stopped.ToString());
        (!stopped.TryGetProperty("confirmation", out var confirmation) || confirmation.ValueKind == JsonValueKind.Null)
            .Should().BeTrue("a safety stop is not held for confirmation");
        await WithDbAsync(async db =>
        {
            (await db.UserSessions.AnyAsync(s => s.UserId == user && s.ExpiresAt > DateTime.UtcNow)).Should().BeFalse();
            var entry = await db.AuditLogs.SingleAsync(a => a.Action == "RevokeUserSessionsCommand");
            entry.UserId.Should().Be(admin);
        });
    }

    [Fact]
    public async Task FindAction_SearchesAndDescribesActions_AndRunsOnlyReadOnlyOnes()
    {
        await _fixture.ResetDatabaseAsync();
        var admin = await UserAsync("admin");
        using var browser = _fixture.CreateAuthenticatedClient(admin, "admin@example.com", "admin");

        var search = await CallAsync(browser, "find_action", new { mode = "search", query = "campaign" });
        var found = search.GetProperty("data").EnumerateArray().Select(a => a.GetProperty("id").GetString()).ToList();
        found.Should().Contain(["admin_pause_campaign", "admin_activate_notification_campaign", "admin_list_notification_campaigns"]);
        found.Should().NotContain("admin_delete_household");

        var described = await CallAsync(browser, "find_action", new { mode = "describe", id = "admin_set_feature_flag" });
        var arguments = described.GetProperty("data").GetProperty("arguments").GetProperty("properties");
        arguments.TryGetProperty("flag", out _).Should().BeTrue();
        arguments.TryGetProperty("enabled", out _).Should().BeTrue();
        described.GetProperty("data").GetProperty("kind").GetString().Should().Be("destructive");

        var flags = await CallAsync(browser, "find_action", new { mode = "run", id = "admin_list_feature_flags" });
        flags.GetProperty("isError").GetBoolean().Should().BeFalse(flags.ToString());

        var write = await CallAsync(browser, "find_action", new { mode = "run", id = "admin_pause_campaign", arguments = new { id = Guid.NewGuid() } });
        write.GetProperty("errorCode").GetString().Should().Be("invalid");
    }

    private async Task<Guid> UserAsync(string role)
    {
        var id = Guid.NewGuid();
        await _fixture.SeedUserAsync(id, $"{role}-{id:N}@example.com", role: role);
        return id;
    }

    private async Task<(Guid Coach, Guid Client)> PairAsync(bool nutrition)
    {
        _fixture.Ai.Reset();
        var coach = await UserAsync("trainer");
        var client = await UserAsync("user");
        await WithDbAsync(async db =>
        {
            db.TrainerClientRelationships.Add(new TrainerClientRelationship
            {
                Id = Guid.CreateVersion7(),
                TrainerId = coach,
                ClientId = client,
                Status = "active",
                CanViewNutrition = nutrition,
                CanViewWorkouts = false,
                CanViewMeasurements = false,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });
        return (coach, client);
    }

    private Task ConsentAsync(Guid user, bool nutrition) => WithDbAsync(async db =>
    {
        db.UserAiConsents.Add(new UserAiConsent
        {
            UserId = user,
            Enabled = nutrition,
            ShareNutrition = nutrition,
            ShareTraining = true,
            ShareBody = false,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    });

    /// <summary>A browser session in which <paramref name="admin"/> views Mizan as <paramref name="user"/>.</summary>
    private async Task<HttpClient> ImpersonatingAsync(Guid admin, Guid user)
    {
        var token = SecureToken.Generate();
        await WithDbAsync(async db =>
        {
            var now = DateTime.UtcNow;
            db.UserSessions.Add(new UserSession
            {
                Id = Guid.CreateVersion7(),
                UserId = user,
                ImpersonatorId = admin,
                TokenHash = SecureToken.Hash(token),
                CreatedAt = now,
                LastSeenAt = now,
                ExpiresAt = now.AddHours(1),
            });
            await db.SaveChangesAsync();
        });
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{ApiTestFixture.SessionCookieName}={token}");
        return client;
    }

    private Task<bool> HouseholdExistsAsync(Guid id)
    {
        var exists = false;
        return WithDbAsync(async db => exists = await db.Households.AnyAsync(h => h.Id == id)).ContinueWith(_ => exists);
    }

    private static async Task<List<string>> ManifestAsync(HttpClient client)
    {
        var manifest = await client.GetFromJsonAsync<JsonElement>("/api/tools?surface=mcp");
        return manifest.GetProperty("tools").EnumerateArray().Select(t => t.GetProperty("name").GetString()!).ToList();
    }

    private static async Task<JsonElement> CallAsync(HttpClient client, string tool, object arguments)
    {
        var response = await client.PostAsJsonAsync($"/api/tools/{tool}/call", new { surface = "mcp", arguments });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task WithDbAsync(Func<MizanDbContext, Task> action)
    {
        using var scope = _fixture.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<MizanDbContext>());
    }

    private async Task<Guid> GrantAsync(Guid user, string[] scopes, bool allowAdmin = false)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var client = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = "mzc_" + SecureToken.Generate()[..16],
            Name = "Test Agent",
            RedirectUris = ["http://localhost:1/cb"],
            Source = OAuthClient.SourceDynamic,
            CreatedAt = DateTime.UtcNow,
        };
        var grant = new OAuthGrant
        {
            Id = Guid.NewGuid(),
            UserId = user,
            ClientId = client.Id,
            Scopes = McpScopes.Normalize(scopes, allowAdmin),
            HouseholdMode = "none",
            HouseholdIds = [],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.OAuthClients.Add(client);
        db.OAuthGrants.Add(grant);
        await db.SaveChangesAsync();
        return grant.Id;
    }

    /// <summary>The way the MCP service calls the API for a connected app: with the admin key, which carries the account role.</summary>
    private HttpClient AppClient(Guid user, Guid grant)
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-admin-api-key");
        client.DefaultRequestHeaders.Add("X-Impersonate-User", user.ToString());
        client.DefaultRequestHeaders.Add("X-Mcp-Grant", grant.ToString());
        return client;
    }
}
