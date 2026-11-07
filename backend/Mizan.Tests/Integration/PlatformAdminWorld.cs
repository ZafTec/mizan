using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;

namespace Mizan.Tests.Integration;

/// <summary>
/// The people, households, plans and ledger rows the platform administration
/// stories share, seeded straight into the database. What the stories assert
/// goes through the API.
/// </summary>
internal sealed class PlatformAdminWorld
{
    private readonly ApiTestFixture _fixture;

    public PlatformAdminWorld(ApiTestFixture fixture) => _fixture = fixture;

    public async Task<(Guid Id, HttpClient Client)> PersonAsync(string label, string role = "user", string? name = null)
    {
        var id = Guid.NewGuid();
        var email = $"{label}-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email, role: role);
        if (name is not null) await DbAsync(async db => { (await db.Users.FindAsync(id))!.Name = name; await db.SaveChangesAsync(); });
        return (id, _fixture.CreateAuthenticatedClient(id, email, role));
    }

    public Task<(Guid Id, HttpClient Client)> AdminAsync() => PersonAsync("admin", role: "admin");

    public async Task<Guid> HouseholdAsync(string name, params Guid[] members)
    {
        var id = Guid.NewGuid();
        await DbAsync(async db =>
        {
            db.Households.Add(new Household { Id = id, Name = name, CreatedBy = members[0], CreatedAt = DateTime.UtcNow });
            foreach (var member in members)
                db.HouseholdMembers.Add(new HouseholdMember { HouseholdId = id, UserId = member, Role = member == members[0] ? "owner" : "member", JoinedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });
        return id;
    }

    public async Task<Guid> BillingPlanAsync(string name, string priceId)
    {
        var id = Guid.NewGuid();
        await DbAsync(async db =>
        {
            db.BillingPlans.Add(new BillingPlan
            {
                Id = id,
                Name = name,
                Interval = BillingIntervals.Month,
                AmountCents = 500,
                Currency = "USD",
                PaddleProductId = "pro_test",
                PaddlePriceId = priceId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });
        return id;
    }

    public Task SubscribeAsync(Guid userId, string priceId) => DbAsync(async db =>
    {
        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Plan = "pro",
            Status = "active",
            PaddlePriceId = priceId,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    });

    public Task LedgerAsync(Guid userId, Guid? householdId, string feature, int tokens, long cost, DateTime at, AiCallOutcome outcome = AiCallOutcome.Succeeded) =>
        DbAsync(async db =>
        {
            db.AiUsageLogs.Add(new AiUsageLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                HouseholdId = householdId,
                Feature = feature,
                Model = "test-model",
                PromptTokens = tokens,
                EstimatedCostMicros = cost,
                Outcome = outcome,
                CreatedAt = at,
            });
            await db.SaveChangesAsync();
        });

    public async Task<T> DbAsync<T>(Func<MizanDbContext, Task<T>> work)
    {
        using var scope = _fixture.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<MizanDbContext>());
    }

    public async Task DbAsync(Func<MizanDbContext, Task> work)
    {
        using var scope = _fixture.Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<MizanDbContext>());
    }

    public static async Task OkAsync(Task<HttpResponseMessage> call)
    {
        var response = await call;
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
    }

    public static async Task<T> ReadAsync<T>(Task<HttpResponseMessage> call)
    {
        var response = await call;
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
