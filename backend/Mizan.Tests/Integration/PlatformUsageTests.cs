using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Ai;
using Mizan.Contracts.Admin;
using Mizan.Domain.Entities;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>The platform usage view and its CSV export (docs/ADMIN.md#usage), and who may open the admin routes.</summary>
[Collection("ApiIntegration")]
public class PlatformUsageTests
{
    private static readonly DateTime Day = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly ApiTestFixture _fixture;
    private readonly PlatformAdminWorld _world;

    public PlatformUsageTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _world = new PlatformAdminWorld(fixture);
    }

    [Fact]
    public async Task UsageGroupedByHouseholdSumsTheRightRows()
    {
        var (admin, home, cabin, ana, _) = await SeedLedgerAsync();

        var usage = await admin.GetFromJsonAsync<PlatformUsageDto>("/api/admin/usage?groupBy=household&from=2026-10-01&to=2026-10-31");

        usage!.GroupBy.Should().Be("household");
        usage.Rows.Should().BeEquivalentTo(new[]
        {
            new PlatformUsageRowDto { Key = home.ToString(), Label = "Home, north", Requests = 3, Tokens = 600, CostMicros = 6_000, Cost = 0.006m, Refusals = 1 },
            new PlatformUsageRowDto { Key = cabin.ToString(), Label = "=Cabin", Requests = 1, Tokens = 50, CostMicros = 500, Cost = 0.0005m, Refusals = 0 },
            new PlatformUsageRowDto { Key = null, Label = "No household", Requests = 1, Tokens = 10, CostMicros = 100, Cost = 0.0001m, Refusals = 0 },
        }, options => options.WithStrictOrdering());
        usage.Total.Should().Match<PlatformUsageRowDto>(t => t.Requests == 5 && t.Tokens == 660 && t.CostMicros == 6_600 && t.Refusals == 1);

        // Narrowed to one person, by feature: only their rows.
        var anaOnly = await admin.GetFromJsonAsync<PlatformUsageDto>($"/api/admin/usage?groupBy=feature&from=2026-10-01&to=2026-10-31&userId={ana}");
        anaOnly!.Rows.Should().BeEquivalentTo(new[]
        {
            new PlatformUsageRowDto { Key = AiFeatures.Planning, Label = AiFeatures.Planning, Requests = 1, Tokens = 400, CostMicros = 4_000, Cost = 0.004m },
            new PlatformUsageRowDto { Key = AiFeatures.Chat, Label = AiFeatures.Chat, Requests = 1, Tokens = 100, CostMicros = 1_000, Cost = 0.001m, Refusals = 1 },
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task TheCsvExportHasTheHeaderAndOneRowPerGroup()
    {
        var (admin, home, cabin, _, _) = await SeedLedgerAsync();

        var response = await admin.GetAsync("/api/admin/usage/export?groupBy=household&from=2026-10-01&to=2026-10-31");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentType.CharSet.Should().Be("utf-8");
        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        var text = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());
        text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Should().Equal(
            "group,key,label,requests,tokens,cost_micros,refusals",
            $"household,{home},\"Home, north\",3,600,6000,1",
            $"household,{cabin},'=Cabin,1,50,500,0",
            "household,,No household,1,10,100,0");
        text.Should().EndWith("\r\n");
    }

    [Fact]
    public async Task ANonAdministratorIsRefusedOnEveryAdminRoute()
    {
        await _fixture.ResetDatabaseAsync();
        var (someone, client) = await _world.PersonAsync("curious");
        var household = await _world.HouseholdAsync("Home", someone);
        var flag = "/api/admin/feature-flags/prices";

        var calls = new (HttpMethod Method, string Path)[]
        {
            (HttpMethod.Get, "/api/admin/feature-flags"),
            (HttpMethod.Put, flag),
            (HttpMethod.Get, $"{flag}/overrides"),
            (HttpMethod.Put, $"{flag}/overrides/{household}"),
            (HttpMethod.Delete, $"{flag}/overrides/{household}"),
            (HttpMethod.Get, "/api/admin/usage-limits?scope=User"),
            (HttpMethod.Get, $"/api/admin/usage-limits/User/{someone}"),
            (HttpMethod.Put, $"/api/admin/usage-limits/User/{someone}"),
            (HttpMethod.Delete, $"/api/admin/usage-limits/User/{someone}"),
            (HttpMethod.Get, "/api/admin/usage"),
            (HttpMethod.Get, "/api/admin/usage/export"),
        };

        foreach (var (method, path) in calls)
        {
            using var request = new HttpRequestMessage(method, path);
            if (method == HttpMethod.Put) request.Content = JsonContent.Create(new { enabled = false, dailyRequests = 99 });
            var response = await client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {path} is for administrators");
        }

        (await _world.DbAsync(db => db.UsageLimitOverrides.CountAsync())).Should().Be(0, "nothing was written");
    }

    /// <summary>
    /// Two households (one named so a spreadsheet would read it as a formula), a
    /// person in none, a refusal, and two rows outside October.
    /// </summary>
    private async Task<(HttpClient Admin, Guid Home, Guid Cabin, Guid Ana, Guid Ben)> SeedLedgerAsync()
    {
        await _fixture.ResetDatabaseAsync();
        var (_, admin) = await _world.AdminAsync();
        var (ana, _) = await _world.PersonAsync("ana", name: "Ana");
        var (ben, _) = await _world.PersonAsync("ben", name: "Ben");
        var (cy, _) = await _world.PersonAsync("cy", name: "Cy");
        var home = await _world.HouseholdAsync("Home, north", ana, ben);
        var cabin = await _world.HouseholdAsync("=Cabin", cy);

        await _world.LedgerAsync(ana, home, AiFeatures.Chat, 100, 1_000, Day);
        await _world.LedgerAsync(ana, home, AiFeatures.Planning, 400, 4_000, Day.AddDays(1));
        await _world.LedgerAsync(ana, home, AiFeatures.Chat, 0, 0, Day.AddDays(1), AiCallOutcome.Refused);
        await _world.LedgerAsync(ben, home, AiFeatures.Chat, 100, 1_000, Day.AddDays(2), AiCallOutcome.ProviderError);
        await _world.LedgerAsync(cy, cabin, AiFeatures.FoodAnalysis, 50, 500, Day);
        await _world.LedgerAsync(cy, null, AiFeatures.Chat, 10, 100, new DateTime(2026, 10, 31, 23, 59, 0, DateTimeKind.Utc));
        await _world.LedgerAsync(ben, home, AiFeatures.Chat, 900, 9_000, new DateTime(2026, 9, 30, 23, 59, 0, DateTimeKind.Utc));
        await _world.LedgerAsync(ben, home, AiFeatures.Chat, 900, 9_000, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        return (admin, home, cabin, ana, ben);
    }
}
