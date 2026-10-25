using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Ai;
using Mizan.Contracts.Admin;
using Mizan.Domain.Entities;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// Allowance overrides by user, household and plan, enforced by the same
/// reservation every AI call makes (docs/AI.md#metering). Fixture tiers: Free is
/// 3 requests and 1,000 tokens a day, Pro 10 and 5,000.
/// </summary>
[Collection("ApiIntegration")]
public class UsageLimitTests
{
    private readonly ApiTestFixture _fixture;
    private readonly PlatformAdminWorld _world;

    public UsageLimitTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _world = new PlatformAdminWorld(fixture);
    }

    [Fact]
    public async Task AUserOverrideLowersTheDailyRequestCap_AndTheNextCallIsRefusedWithTheCapNamed()
    {
        await _fixture.ResetDatabaseAsync();
        _fixture.Ai.Reset();
        var (_, admin) = await _world.AdminAsync();
        var (me, client) = await _world.PersonAsync("chatty");
        var home = await _world.HouseholdAsync("Home", me);
        // A household cap the user's own override sits above: the user's value wins, the household fills the rest.
        await PlatformAdminWorld.OkAsync(admin.PutAsJsonAsync($"/api/admin/usage-limits/Household/{home}",
            new SetUsageLimitRequest(null, 2, 800, null, null, "Shared allowance")));

        var subject = await PlatformAdminWorld.ReadAsync<UsageLimitSubjectDto>(admin.PutAsJsonAsync($"/api/admin/usage-limits/User/{me}",
            new SetUsageLimitRequest("Personal", 1, null, null, null, "Testing a tight cap")));

        var personal = subject.Effective.Single(e => e.Line == "Personal");
        personal.DailyRequests.Should().Be(new SourcedLimitDto(1, "User"));
        personal.DailyTokens.Should().Be(new SourcedLimitDto(800, "Household"));
        personal.DailyCostMicros.Should().Be(new SourcedLimitDto(null, "Default"));
        subject.Effective.Single(e => e.Line == "Planning").DailyRequests.Should().Be(new SourcedLimitDto(2, "Household"));
        subject.Overrides.Should().ContainSingle().Which.Note.Should().Be("Testing a tight cap");

        _fixture.Ai.Reply("Here is your answer.");
        (await client.PostAsJsonAsync("/api/Ai/chat", new { message = "How am I doing?" })).StatusCode.Should().Be(HttpStatusCode.OK);

        var refused = await client.PostAsJsonAsync("/api/Ai/chat", new { message = "And now?" });
        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errorCode").GetString().Should().Be("ai_quota_exceeded");
        problem.GetProperty("cap").GetString().Should().Be("daily_requests");
        problem.GetProperty("error").GetString().Should().StartWith("You have reached your daily limit of AI requests.");
        _fixture.Ai.Calls.Should().HaveCount(1, "the refused call never reached the provider");

        // The refusal is in the ledger at no cost, and it does not count as a request.
        var ledger = await _world.DbAsync(db => db.AiUsageLogs.AsNoTracking().OrderBy(l => l.CreatedAt).ToListAsync());
        ledger.Select(l => l.Outcome).Should().Equal(AiCallOutcome.Succeeded, AiCallOutcome.Refused);
        ledger.Should().OnlyContain(l => l.HouseholdId == home);
        ledger[1].Should().Match<AiUsageLog>(l => l.PromptTokens == 0 && l.EstimatedCostMicros == 0 && l.Feature == AiFeatures.Chat);
        var usage = await client.GetFromJsonAsync<JsonElement>("/api/Ai/usage");
        usage.GetProperty("today").GetProperty("requestsUsed").GetInt32().Should().Be(1);
        usage.GetProperty("today").GetProperty("requestLimit").GetInt32().Should().Be(1);

        // Removing the user's override hands the cap back to the household.
        await PlatformAdminWorld.OkAsync(admin.DeleteAsync($"/api/admin/usage-limits/User/{me}?line=Personal"));
        _fixture.Ai.Reply("Back again.");
        (await client.PostAsJsonAsync("/api/Ai/chat", new { message = "Once more?" })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task APlanOverrideAppliesToEverySubscriberOfThatPlan()
    {
        await _fixture.ResetDatabaseAsync();
        _fixture.Ai.Reset();
        var (_, admin) = await _world.AdminAsync();
        var family = await _world.BillingPlanAsync("Family monthly", "pri_family");
        await _world.BillingPlanAsync("Solo monthly", "pri_solo");
        var (first, firstClient) = await _world.PersonAsync("first");
        var (second, secondClient) = await _world.PersonAsync("second");
        var (solo, soloClient) = await _world.PersonAsync("solo");
        await _world.SubscribeAsync(first, "pri_family");
        await _world.SubscribeAsync(second, "pri_family");
        await _world.SubscribeAsync(solo, "pri_solo");

        await PlatformAdminWorld.OkAsync(admin.PutAsJsonAsync($"/api/admin/usage-limits/Plan/{family}",
            new SetUsageLimitRequest(null, 4, null, null, 50_000, null)));

        // Both subscribers of the plan get its cap; the other plan keeps the Pro default of 10.
        foreach (var client in new[] { firstClient, secondClient })
        {
            var usage = await client.GetFromJsonAsync<JsonElement>("/api/Ai/usage");
            usage.GetProperty("today").GetProperty("requestLimit").GetInt32().Should().Be(4);
            usage.GetProperty("planning").GetProperty("requestLimit").GetInt32().Should().Be(4);
        }
        (await soloClient.GetFromJsonAsync<JsonElement>("/api/Ai/usage"))
            .GetProperty("today").GetProperty("requestLimit").GetInt32().Should().Be(10);

        // A subscriber who already spent the plan's monthly cost cap is refused, with the cap named.
        await _world.LedgerAsync(second, null, AiFeatures.Chat, 100, 50_000, DateTime.UtcNow.AddMinutes(-5));
        var refused = await secondClient.PostAsJsonAsync("/api/Ai/chat", new { message = "Hello?" });
        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cap").GetString().Should().Be("monthly_cost");

        // The plans tab lists every plan, the family plan's caps sourced from it.
        var plans = await admin.GetFromJsonAsync<List<UsageLimitSubjectDto>>("/api/admin/usage-limits?scope=Plan");
        plans!.Select(p => p.Name).Should().BeEquivalentTo(["Family monthly", "Solo monthly"]);
        var personal = plans.Single(p => p.SubjectId == family).Effective.Single(e => e.Line == "Personal");
        personal.DailyRequests.Should().Be(new SourcedLimitDto(4, "Plan"));
        personal.MonthlyCostMicros.Should().Be(new SourcedLimitDto(50_000, "Plan"));
        personal.DailyTokens.Should().Be(new SourcedLimitDto(5000, "Default"));
        plans.Single(p => p.SubjectId != family).Effective.Single(e => e.Line == "Personal").DailyRequests
            .Should().Be(new SourcedLimitDto(10, "Default"));
    }
}
