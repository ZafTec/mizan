using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Mizan.Application.Ai.Tools;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Mcp;
using Mizan.Domain.Ai;
using Mizan.Domain.Entities;
using Mizan.Domain.Identity;
using Mizan.Infrastructure.Ai;
using Mizan.Infrastructure.Data;
using Moq;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// What a connected app can and cannot reach. The MCP service names the
/// connection on each call; the API loads it and enforces household and data
/// limits itself, so a service bug cannot widen a user's choice.
/// </summary>
[Collection("ApiIntegration")]
public class McpConnectionsTests
{
    private readonly ApiTestFixture _fixture;

    public McpConnectionsTests(ApiTestFixture fixture) => _fixture = fixture;

    // ---- households ----

    [Fact]
    public async Task ASelectedHousehold_IsTheOnlyOneAnAppCanReach()
    {
        var w = await WorldAsync();
        var grant = await GrantAsync(w.User, new[] { "planning:write", "households:read" }, "selected", w.HouseholdA);

        using var app = AppClient(w.User, grant);
        (await app.GetAsync($"/api/ShoppingLists/{w.ListInA}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await app.GetAsync($"/api/ShoppingLists/{w.ListInB}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await app.GetAsync($"/api/ShoppingLists/{w.PersonalList}")).StatusCode.Should().Be(HttpStatusCode.OK,
            "the user's own lists are not household data");
    }

    [Fact]
    public async Task NoHouseholds_MeansPersonalDataOnly()
    {
        var w = await WorldAsync();
        var grant = await GrantAsync(w.User, new[] { "planning:write" }, "none");

        using var app = AppClient(w.User, grant);
        (await app.GetAsync($"/api/ShoppingLists/{w.ListInA}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await app.GetAsync($"/api/ShoppingLists/{w.ListInB}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await app.GetAsync($"/api/ShoppingLists/{w.PersonalList}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AllHouseholds_AndTheWebApp_SeeEverythingTheUserBelongsTo()
    {
        var w = await WorldAsync();
        var grant = await GrantAsync(w.User, new[] { "planning:write" }, "all");

        using var app = AppClient(w.User, grant);
        (await app.GetAsync($"/api/ShoppingLists/{w.ListInA}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await app.GetAsync($"/api/ShoppingLists/{w.ListInB}")).StatusCode.Should().Be(HttpStatusCode.OK);

        using var browser = _fixture.CreateAuthenticatedClient(w.User, w.Email);
        (await browser.GetAsync($"/api/ShoppingLists/{w.ListInA}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await browser.GetAsync($"/api/ShoppingLists/{w.ListInB}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnApp_CannotFileAListOrPlanUnderAHouseholdItWasNotGiven()
    {
        var w = await WorldAsync();
        var grant = await GrantAsync(w.User, new[] { "planning:write" }, "selected", w.HouseholdA);
        using var app = AppClient(w.User, grant);

        (await app.PostAsJsonAsync("/api/ShoppingLists", new { name = "Sneaky", householdId = w.HouseholdB }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await app.PostAsJsonAsync("/api/ShoppingLists", new { name = "Fine", householdId = w.HouseholdA }))
            .StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

        var plan = new
        {
            name = "Week", startDate = "2026-10-05", endDate = "2026-10-11", recipes = Array.Empty<object>(),
        };
        (await app.PostAsJsonAsync("/api/MealPlans", new { plan.name, plan.startDate, plan.endDate, plan.recipes, householdId = w.HouseholdB }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await app.PostAsJsonAsync("/api/MealPlans", new { plan.name, plan.startDate, plan.endDate, plan.recipes, householdId = w.HouseholdA }))
            .StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
    }

    [Fact]
    public async Task AnyUser_CannotFileAListOrPlanUnderAHouseholdTheyDoNotBelongTo()
    {
        var w = await WorldAsync();
        var stranger = Guid.NewGuid();
        await _fixture.SeedUserAsync(stranger, $"stranger-{stranger:N}@example.com");
        using var browser = _fixture.CreateAuthenticatedClient(stranger, "stranger@example.com");

        (await browser.PostAsJsonAsync("/api/ShoppingLists", new { name = "Injected", householdId = w.HouseholdA }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await browser.PostAsJsonAsync("/api/MealPlans", new
        {
            name = "Injected", startDate = "2026-10-05", endDate = "2026-10-11",
            recipes = Array.Empty<object>(), householdId = w.HouseholdA,
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        (await db.ShoppingLists.AnyAsync(l => l.UserId == stranger)).Should().BeFalse();
        (await db.MealPlans.AnyAsync(p => p.UserId == stranger)).Should().BeFalse();
    }

    [Fact]
    public async Task TheHouseholdList_ShowsAnAppOnlyWhatItWasGiven()
    {
        var w = await WorldAsync();

        var onlyA = await GrantAsync(w.User, new[] { "households:read" }, "selected", w.HouseholdA);
        using (var app = AppClient(w.User, onlyA))
        {
            var mine = await app.GetFromJsonAsync<JsonElement>("/api/Households/mine");
            mine.GetProperty("households").EnumerateArray().Select(h => h.GetProperty("id").GetGuid())
                .Should().BeEquivalentTo(new[] { w.HouseholdA });
            (await app.GetAsync($"/api/Households/{w.HouseholdB}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await app.GetAsync($"/api/Households/{w.HouseholdA}")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var none = await GrantAsync(w.User, new[] { "households:read" }, "none", clientName: "Another app");
        using (var app = AppClient(w.User, none))
        {
            var mine = await app.GetFromJsonAsync<JsonElement>("/api/Households/mine");
            mine.GetProperty("households").GetArrayLength().Should().Be(0);
        }
    }

    [Fact]
    public async Task AnApp_CannotManageAHouseholdItCannotSee()
    {
        var w = await WorldAsync();
        var grant = await GrantAsync(w.User, new[] { "households:write" }, "selected", w.HouseholdA);
        using var app = AppClient(w.User, grant);

        (await app.PostAsJsonAsync($"/api/Households/{w.HouseholdB}/invitations", new { email = "x@example.com", role = "member" }))
            .StatusCode.Should().NotBe(HttpStatusCode.OK);
        (await app.PostAsync($"/api/Households/{w.HouseholdB}/leave", null)).StatusCode.Should().NotBe(HttpStatusCode.OK);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        (await db.HouseholdMembers.AnyAsync(m => m.HouseholdId == w.HouseholdB && m.UserId == w.User))
            .Should().BeTrue("the leave attempt must not have removed the user");
    }

    // ---- the header and the grant behind it ----

    [Fact]
    public async Task TheGrantHeader_IsRejectedWhenTheGrantIsRevokedGarbledOrAnotherUsers()
    {
        var w = await WorldAsync();
        var grant = await GrantAsync(w.User, new[] { "planning:read" }, "all");
        var other = Guid.NewGuid();
        await _fixture.SeedUserAsync(other, $"o-{other:N}@example.com");

        using (var app = AppClient(other, grant))
            (await app.GetAsync("/api/ShoppingLists")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using (var app = AppClient(w.User, Guid.NewGuid()))
            (await app.GetAsync("/api/ShoppingLists")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using (var garbled = _fixture.CreateClient())
        {
            garbled.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
            garbled.DefaultRequestHeaders.Add("X-Impersonate-User", w.User.ToString());
            garbled.DefaultRequestHeaders.Add("X-Mcp-Grant", "not-a-guid");
            (await garbled.GetAsync("/api/ShoppingLists")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        using (var app = AppClient(w.User, grant))
            (await app.GetAsync("/api/ShoppingLists")).StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
            await db.OAuthGrants.Where(g => g.Id == grant).ExecuteUpdateAsync(s => s.SetProperty(g => g.RevokedAt, DateTime.UtcNow));
        }

        using (var app = AppClient(w.User, grant))
            (await app.GetAsync("/api/ShoppingLists")).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "revoking a connection ends it on the very next call");
    }

    [Fact]
    public async Task ReducingAGrant_TakesEffectOnTheNextCall()
    {
        var w = await WorldAsync();
        var grant = await GrantAsync(w.User, new[] { "planning:write" }, "all");

        using var app = AppClient(w.User, grant);
        (await app.GetAsync($"/api/ShoppingLists/{w.ListInB}")).StatusCode.Should().Be(HttpStatusCode.OK);

        using var browser = _fixture.CreateAuthenticatedClient(w.User, w.Email);
        (await browser.PatchAsJsonAsync($"/api/McpConnections/{grant}", new
        {
            householdMode = "selected", householdIds = new[] { w.HouseholdA },
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await app.GetAsync($"/api/ShoppingLists/{w.ListInB}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await app.GetAsync($"/api/ShoppingLists/{w.ListInA}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- the connections API ----

    [Fact]
    public async Task Connections_ListUsageAndCanBeNarrowedButNeverWidened()
    {
        var w = await WorldAsync();
        var grant = await GrantAsync(w.User, new[] { "nutrition:write", "training:read" }, "selected", w.HouseholdA);
        await LogUsageAsync(w.User, grant, "log_food", success: true);
        await LogUsageAsync(w.User, grant, "log_food", success: false);

        using var browser = _fixture.CreateAuthenticatedClient(w.User, w.Email);
        var list = await browser.GetFromJsonAsync<JsonElement>("/api/McpConnections");
        var connection = list.EnumerateArray().Single();
        connection.GetProperty("clientName").GetString().Should().Be("Test Assistant");
        connection.GetProperty("calls30Days").GetInt32().Should().Be(2);
        connection.GetProperty("failed30Days").GetInt32().Should().Be(1);
        connection.GetProperty("households").EnumerateArray().Select(h => h.GetProperty("name").GetString())
            .Should().BeEquivalentTo(new[] { "Household A" });

        // Narrowing works.
        (await browser.PatchAsJsonAsync($"/api/McpConnections/{grant}", new { scopes = new[] { "nutrition:read" } }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var narrowed = (await browser.GetFromJsonAsync<JsonElement>("/api/McpConnections")).EnumerateArray().Single();
        narrowed.GetProperty("scopes").EnumerateArray().Select(s => s.GetString()).Should().BeEquivalentTo(new[] { "nutrition:read" });

        // Widening does not, whether by scope or by household.
        (await browser.PatchAsJsonAsync($"/api/McpConnections/{grant}", new { scopes = new[] { "nutrition:write" } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.PatchAsJsonAsync($"/api/McpConnections/{grant}", new { scopes = new[] { "body:read" } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.PatchAsJsonAsync($"/api/McpConnections/{grant}", new { householdMode = "all" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.PatchAsJsonAsync($"/api/McpConnections/{grant}", new { householdMode = "selected", householdIds = new[] { w.HouseholdB } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await browser.PatchAsJsonAsync($"/api/McpConnections/{grant}", new { scopes = Array.Empty<string>() }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TheScopeCatalog_OffersAdminOnlyToAdministrators()
    {
        await _fixture.ResetDatabaseAsync();
        var plain = Guid.NewGuid();
        var admin = Guid.NewGuid();
        await _fixture.SeedUserAsync(plain, $"p-{plain:N}@example.com");
        await _fixture.SeedUserAsync(admin, $"a-{admin:N}@example.com", role: "admin");

        using var plainClient = _fixture.CreateAuthenticatedClient(plain, "p@example.com");
        var plainGroups = await plainClient.GetFromJsonAsync<JsonElement>("/api/McpConnections/scopes");
        var names = plainGroups.EnumerateArray().Select(g => g.GetProperty("group").GetString()).ToList();
        names.Should().Contain("nutrition").And.Contain("ai").And.NotContain("admin");
        var nutrition = plainGroups.EnumerateArray().Single(g => g.GetProperty("group").GetString() == "nutrition");
        nutrition.GetProperty("readScope").GetString().Should().Be("nutrition:read");
        nutrition.GetProperty("writeScope").GetString().Should().Be("nutrition:write");

        using var adminClient = _fixture.CreateAuthenticatedClient(admin, "a@example.com", "admin");
        (await adminClient.GetFromJsonAsync<JsonElement>("/api/McpConnections/scopes")).EnumerateArray()
            .Select(g => g.GetProperty("group").GetString()).Should().Contain("admin");
    }

    [Fact]
    public async Task Connections_BelongToTheirOwner()
    {
        var w = await WorldAsync();
        var grant = await GrantAsync(w.User, new[] { "profile:read" }, "none");
        var stranger = Guid.NewGuid();
        await _fixture.SeedUserAsync(stranger, $"s-{stranger:N}@example.com");
        using var browser = _fixture.CreateAuthenticatedClient(stranger, "s@example.com");

        (await browser.GetFromJsonAsync<JsonElement>("/api/McpConnections")).GetArrayLength().Should().Be(0);
        (await browser.PatchAsJsonAsync($"/api/McpConnections/{grant}", new { scopes = new[] { "profile:read" } }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await browser.DeleteAsync($"/api/McpConnections/{grant}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Disconnecting_EndsTheGrantAndEveryTokenOfIt()
    {
        var w = await WorldAsync();
        var grant = await GrantAsync(w.User, new[] { "profile:read" }, "none");
        using var browser = _fixture.CreateAuthenticatedClient(w.User, w.Email);

        (await browser.DeleteAsync($"/api/McpConnections/{grant}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await browser.GetFromJsonAsync<JsonElement>("/api/McpConnections")).GetArrayLength().Should().Be(0);
        (await browser.DeleteAsync($"/api/McpConnections/{grant}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OnlyCallsThatReadOrChangeData_CountTowardTheFreePlanCap()
    {
        var w = await WorldAsync();
        var access = await _fixture.CreateMcpAccessAsync(w.User);
        await LogUsageAsync(w.User, access.GrantId, "search_foods", success: true);
        await LogUsageAsync(w.User, access.GrantId, "log_food", success: false);
        for (var i = 0; i < 5; i++) await LogUsageAsync(w.User, access.GrantId, "weekly_review", success: true, kind: "prompt");
        await LogUsageAsync(w.User, access.GrantId, "mizan://profile", success: true, kind: "resource");

        using var service = _fixture.CreateClient();
        service.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
        var response = await service.PostAsJsonAsync("/api/oauth/introspect", new { token = access.Token, audience = "mcp" });
        var info = await response.Content.ReadFromJsonAsync<JsonElement>();

        info.GetProperty("monthlyLimit").GetInt32().Should().Be(15);
        info.GetProperty("usedThisMonth").GetInt32().Should().Be(2, "a tool and a resource read count; a failure and prompts do not");
    }

    [Fact]
    public async Task Usage_IsRecordedAgainstTheConnection_AndAppearsInAnalytics()
    {
        var w = await WorldAsync();
        var grant = await GrantAsync(w.User, new[] { "profile:read" }, "none");

        using (var service = AppClient(w.User, grant, includeGrant: false))
        {
            var response = await service.PostAsJsonAsync("/api/McpConnections/usage", new
            {
                grantId = grant, kind = "resource", toolName = "mizan://profile", success = true, executionTimeMs = 12,
            });
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var foreign = await service.PostAsJsonAsync("/api/McpConnections/usage", new
            {
                grantId = Guid.NewGuid(), toolName = "x", success = true, executionTimeMs = 1,
            });
            foreign.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "a call can only be logged against the user's own connection");
        }

        using var browser = _fixture.CreateAuthenticatedClient(w.User, w.Email);
        var analytics = await browser.GetFromJsonAsync<JsonElement>("/api/McpConnections/analytics");
        analytics.GetProperty("overview").GetProperty("totalCalls").GetInt32().Should().Be(1);
        analytics.GetProperty("overview").GetProperty("uniqueClientsUsed").GetInt32().Should().Be(1);
        analytics.GetProperty("clientUsage").EnumerateArray().Single().GetProperty("clientName").GetString().Should().Be("Test Assistant");

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        (await db.McpUsageLogs.SingleAsync()).Kind.Should().Be("resource");
    }

    // ---- data axes and AI tools ----

    [Fact]
    public async Task DataAccessPolicy_IntersectsWithWhatTheAppWasGiven()
    {
        var w = await WorldAsync();
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var app = AppUser(w.User, "nutrition:read", "body:read");
        var policy = new DataAccessPolicy(db, app);

        var own = await policy.ReadableAxesAsync(w.User, w.User, AccessPurpose.Display);
        own.Should().BeEquivalentTo(new[] { DataAxis.Nutrition, DataAxis.Body });

        (await new DataAccessPolicy(db, AppUser(w.User, McpScopes.Full)).ReadableAxesAsync(w.User, w.User, AccessPurpose.Display))
            .Should().HaveCount(3);
        (await new DataAccessPolicy(db).ReadableAxesAsync(w.User, w.User, AccessPurpose.Display))
            .Should().HaveCount(3, "the web app has no grant and keeps full authority");
    }

    [Fact]
    public async Task DataAccessPolicy_ReadingAClient_NeedsTheCoachingScopeToo()
    {
        var w = await WorldAsync();
        var client = Guid.NewGuid();
        await _fixture.SeedUserAsync(client, $"client-{client:N}@example.com");
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        db.TrainerClientRelationships.Add(new TrainerClientRelationship
        {
            Id = Guid.NewGuid(), TrainerId = w.User, ClientId = client, Status = "active",
            CanViewNutrition = true, CanViewWorkouts = true, CanViewMeasurements = true, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        (await new DataAccessPolicy(db, AppUser(w.User, "nutrition:read")).ReadableAxesAsync(w.User, client, AccessPurpose.Display))
            .Should().BeEmpty("nutrition:read alone does not include coaching");
        (await new DataAccessPolicy(db, AppUser(w.User, "nutrition:read", "trainer:read")).ReadableAxesAsync(w.User, client, AccessPurpose.Display))
            .Should().BeEquivalentTo(new[] { DataAxis.Nutrition });
    }

    [Fact]
    public async Task AiTools_AreHeldToTheAppsOwnScopes()
    {
        var w = await WorldAsync();
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var mediator = new Mock<MediatR.IMediator>();
        var runner = new AiToolRunner(mediator.Object, db, NullLogger<AiToolRunner>.Instance, AppUser(w.User, "body:read", "ai:use"));

        var result = await runner.RunAsync(
            new AiToolCall("1", "log_measurement", """{"weightKg": 80}"""), new AiToolContext(w.User));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("not allowed to change");
        mediator.VerifyNoOtherCalls();
    }

    // ---- helpers ----

    private sealed record World(Guid User, string Email, Guid HouseholdA, Guid HouseholdB, Guid ListInA, Guid ListInB, Guid PersonalList);

    private async Task<World> WorldAsync()
    {
        await _fixture.ResetDatabaseAsync();
        var user = Guid.NewGuid();
        var email = $"conn-{user:N}@example.com";
        await _fixture.SeedUserAsync(user, email);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var a = new Household { Id = Guid.NewGuid(), Name = "Household A", CreatedBy = user, CreatedAt = DateTime.UtcNow };
        var b = new Household { Id = Guid.NewGuid(), Name = "Household B", CreatedBy = user, CreatedAt = DateTime.UtcNow };
        db.Households.AddRange(a, b);
        db.HouseholdMembers.AddRange(
            new HouseholdMember { HouseholdId = a.Id, UserId = user, Role = "owner", JoinedAt = DateTime.UtcNow },
            new HouseholdMember { HouseholdId = b.Id, UserId = user, Role = "member", JoinedAt = DateTime.UtcNow });

        ShoppingList List(Guid? household, string name) => new()
        {
            Id = Guid.NewGuid(), Name = name, UserId = user, HouseholdId = household,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        var inA = List(a.Id, "In A");
        var inB = List(b.Id, "In B");
        var personal = List(null, "Mine");
        db.ShoppingLists.AddRange(inA, inB, personal);
        await db.SaveChangesAsync();

        return new World(user, email, a.Id, b.Id, inA.Id, inB.Id, personal.Id);
    }

    private async Task<Guid> GrantAsync(Guid user, string[] scopes, string mode, Guid? household = null, string clientName = "Test Assistant")
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var client = new OAuthClient
        {
            Id = Guid.NewGuid(), ClientId = "mzc_" + SecureToken.Generate()[..16], Name = clientName,
            RedirectUris = ["http://localhost:1/cb"], Source = OAuthClient.SourceDynamic, CreatedAt = DateTime.UtcNow,
        };
        var grant = new OAuthGrant
        {
            Id = Guid.NewGuid(), UserId = user, ClientId = client.Id,
            Scopes = McpScopes.Normalize(scopes, allowAdmin: false), HouseholdMode = mode,
            HouseholdIds = household is { } h ? [h] : [], CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        db.OAuthClients.Add(client);
        db.OAuthGrants.Add(grant);
        await db.SaveChangesAsync();
        return grant.Id;
    }

    private async Task LogUsageAsync(Guid user, Guid grant, string tool, bool success, string kind = "tool")
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        db.McpUsageLogs.Add(new McpUsageLog
        {
            Id = Guid.NewGuid(), UserId = user, GrantId = grant, ToolName = tool, Success = success, Kind = kind,
            ExecutionTimeMs = 5, Timestamp = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>The way the MCP service calls the API for a connected app.</summary>
    private HttpClient AppClient(Guid user, Guid grant, bool includeGrant = true)
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
        client.DefaultRequestHeaders.Add("X-Impersonate-User", user.ToString());
        if (includeGrant) client.DefaultRequestHeaders.Add("X-Mcp-Grant", grant.ToString());
        return client;
    }

    private static ICurrentUserService AppUser(Guid user, params string[] scopes)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.SetupGet(u => u.UserId).Returns(user);
        mock.SetupGet(u => u.Grant).Returns(new GrantContext(Guid.NewGuid(), Guid.NewGuid(), scopes, "none", []));
        return mock.Object;
    }
}
