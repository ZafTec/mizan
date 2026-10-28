using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Ai;
using Mizan.Contracts.Admin;
using Mizan.Contracts.Planning;
using Mizan.Domain.Features;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// Feature flags with household overrides, through the admin API and the gated
/// routes they switch (docs/ADMIN.md#feature-flags).
/// </summary>
[Collection("ApiIntegration")]
public class FeatureFlagTests
{
    private readonly ApiTestFixture _fixture;
    private readonly PlatformAdminWorld _world;

    public FeatureFlagTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _world = new PlatformAdminWorld(fixture);
    }

    [Fact]
    public async Task AHouseholdOverrideTurnsPlanGenerationOffForItsMembers_AndLeavesAnotherHouseholdAlone()
    {
        await _fixture.ResetDatabaseAsync();
        _fixture.Ai.Reset();
        await _fixture.SeedFoodAsync("Eggs", 143, 12.6m, 0.7m, 9.5m);
        var (_, admin) = await _world.AdminAsync();
        var (owner, ownerClient) = await _world.PersonAsync("owner");
        var (member, memberClient) = await _world.PersonAsync("member");
        var (neighbour, neighbourClient) = await _world.PersonAsync("neighbour");
        var home = await _world.HouseholdAsync("Home", owner, member);
        var nextDoor = await _world.HouseholdAsync("Next door", neighbour);

        var set = await PlatformAdminWorld.ReadAsync<FeatureFlagOverrideDto>(admin.PutAsJsonAsync(
            $"/api/admin/feature-flags/{FeatureFlags.PlanningGenerate}/overrides/{home}", new SetFeatureFlagRequest(false)));
        set.Should().Match<FeatureFlagOverrideDto>(o => o.HouseholdName == "Home" && o.MemberCount == 2 && !o.Enabled);

        // Both members of the household are refused, with the message every surface shows, before any provider call.
        foreach (var client in new[] { ownerClient, memberClient })
        {
            var refused = await client.PostAsJsonAsync("/api/MealPlans/generate", Request());
            refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var problem = await refused.Content.ReadFromJsonAsync<JsonElement>();
            problem.GetProperty("errorCode").GetString().Should().Be("feature_disabled");
            problem.GetProperty("error").GetString().Should().Be("This feature is turned off for your household.");
            problem.GetProperty("feature").GetString().Should().Be(FeatureFlags.PlanningGenerate);
        }
        _fixture.Ai.Calls.Should().BeEmpty();

        // The other household still plans, and its call is recorded against its household.
        _fixture.Ai.Reply(OneDay);
        var drafted = await neighbourClient.PostAsJsonAsync("/api/MealPlans/generate", Request());
        drafted.StatusCode.Should().Be(HttpStatusCode.OK, await drafted.Content.ReadAsStringAsync());
        var ledger = await _world.DbAsync(db => db.AiUsageLogs.AsNoTracking().ToListAsync());
        ledger.Should().ContainSingle().Which.Should().Match<Domain.Entities.AiUsageLog>(l =>
            l.UserId == neighbour && l.HouseholdId == nextDoor && l.Feature == AiFeatures.Planning);

        // The list says one household answers differently; the override is listed, and clearing it restores the default.
        var flags = await admin.GetFromJsonAsync<List<FeatureFlagDto>>("/api/admin/feature-flags");
        flags!.Select(f => f.Key).Should().Equal(FeatureFlags.All.Select(f => f.Key));
        flags.Single(f => f.Key == FeatureFlags.PlanningGenerate).Should().Match<FeatureFlagDto>(f =>
            f.DefaultEnabled && f.OverrideCount == 1 && f.DisabledOverrides == 1);
        var overrides = await admin.GetFromJsonAsync<List<FeatureFlagOverrideDto>>($"/api/admin/feature-flags/{FeatureFlags.PlanningGenerate}/overrides");
        overrides!.Should().ContainSingle(o => o.HouseholdId == home);

        await PlatformAdminWorld.OkAsync(admin.DeleteAsync($"/api/admin/feature-flags/{FeatureFlags.PlanningGenerate}/overrides/{home}"));
        _fixture.Ai.Reply(OneDay);
        (await memberClient.PostAsJsonAsync("/api/MealPlans/generate", Request())).StatusCode.Should().Be(HttpStatusCode.OK);

        // Each change was audited, and a key outside the catalog is refused.
        var audited = await _world.DbAsync(db => db.AuditLogs.AsNoTracking().Select(a => a.Action).ToListAsync());
        audited.Should().Contain(["SetFeatureFlagOverrideCommand", "ClearFeatureFlagOverrideCommand"]);
        (await admin.PutAsJsonAsync("/api/admin/feature-flags/planning.anything", new SetFeatureFlagRequest(false)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TheDefaultOffHidesTheFeatureForEveryoneWithoutAnOverride()
    {
        await _fixture.ResetDatabaseAsync();
        var (_, admin) = await _world.AdminAsync();
        var (_, loner) = await _world.PersonAsync("loner");
        var (member, memberClient) = await _world.PersonAsync("member");
        var (keen, keenClient) = await _world.PersonAsync("keen");
        await _world.HouseholdAsync("Plain", member);
        var keenHome = await _world.HouseholdAsync("Keen", keen);
        var food = await _fixture.SeedFoodAsync("Teff", 360, 13, 73, 2.4m);

        // Read once while it is on, so a cached answer would show if the write did not clear it.
        (await loner.GetAsync($"/api/Foods/{food.Id}/price")).StatusCode.Should().Be(HttpStatusCode.OK);

        await PlatformAdminWorld.OkAsync(admin.PutAsJsonAsync($"/api/admin/feature-flags/{FeatureFlags.Prices}/overrides/{keenHome}", new SetFeatureFlagRequest(true)));
        var flag = await PlatformAdminWorld.ReadAsync<FeatureFlagDto>(admin.PutAsJsonAsync($"/api/admin/feature-flags/{FeatureFlags.Prices}", new SetFeatureFlagRequest(false)));
        flag.Should().Match<FeatureFlagDto>(f => !f.DefaultEnabled && f.CatalogDefault && f.UpdatedAt != null && f.EnabledOverrides == 1);

        // Someone in no household and a household without an override are both refused.
        foreach (var client in new[] { loner, memberClient })
        {
            var refused = await client.GetAsync($"/api/Foods/{food.Id}/price");
            refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().Should().Be("feature_disabled");
            (await client.PostAsJsonAsync($"/api/Foods/{food.Id}/price-reports", new { suggestedPrice = 10, quantity = 1, unit = "kg" }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // The household that kept it on still sees prices.
        (await keenClient.GetAsync($"/api/Foods/{food.Id}/price")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static GenerateMealPlanRequest Request() => new()
    {
        StartDate = new DateOnly(2026, 11, 2),
        DurationDays = 1,
        Preference = "something simple",
    };

    private const string OneDay = """
        {"name":"One day","note":null,"prepSessions":[],"days":[{"dayIndex":0,"entries":[
          {"slot":"BREAKFAST","kind":"food","foodName":"Eggs","recipeId":null,"grams":120,"servings":null,"note":null,"prepSessionIndex":null},
          {"slot":"LUNCH","kind":"text","foodName":"Market salad","recipeId":null,"grams":null,"servings":null,"note":null,"prepSessionIndex":null}]}]}
        """;
}
