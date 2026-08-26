using System.Data.Common;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// Logging speed is on the "must never break" list, and nothing was watching
/// it. This watches it.
///
/// The budget counts database round trips rather than milliseconds, because a
/// wall-clock assertion in CI is a flake generator and tells you nothing about
/// why it got slower. A round-trip count is deterministic and fails on the
/// exact commit that adds a query to the hot path.
///
/// If a change genuinely needs another query, raise the number here and say
/// why in the commit. That conversation is the point.
/// </summary>
[Collection("ApiIntegration")]
public class LoggingBudgetTests
{
    /// <summary>
    /// What logging a meal costs today: the insert, the counter upsert, the
    /// streak read and write, the already-earned lookup, the counter read for
    /// the threshold check, and the transaction statements around them.
    ///
    /// Exact rather than approximate, on purpose. A number with slack in it
    /// absorbs the first added query silently, which is precisely the
    /// regression this is here to catch. If a change genuinely needs another
    /// round trip, raise this and say why in the commit - that conversation is
    /// the point.
    /// </summary>
    private const int MealBudget = 8;

    private readonly ApiTestFixture _fixture;

    public LoggingBudgetTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task LoggingAMealStaysInsideItsRoundTripBudget()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await UserAsync();
        var food = await _fixture.SeedFoodAsync($"Oats {Guid.NewGuid():N}", 380, 13, 67, 7);

        // Warm-up: the first request of a process pays for model building and
        // connection setup, which is not what this measures.
        await LogAsync(client, food.Id);

        using var counter = _fixture.CountCommands();
        await LogAsync(client, food.Id);

        counter.Count.Should().BeLessThanOrEqualTo(
            MealBudget,
            "logging is the one path that must stay fast; it issued {0} round trips",
            counter.Count);
    }

    /// <summary>
    /// The regression this guards: achievement thresholds used to be checked
    /// with COUNT(*) over the user's whole diary, so the cost of logging grew
    /// with every meal ever logged. Counters made it flat, and flat is the
    /// property worth asserting.
    /// </summary>
    [Fact]
    public async Task TheCostOfLoggingDoesNotGrowWithHistory()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await UserAsync();
        var food = await _fixture.SeedFoodAsync($"Rice {Guid.NewGuid():N}", 130, 2.7m, 28, 0.3m);

        await LogAsync(client, food.Id);

        using (var first = _fixture.CountCommands())
        {
            await LogAsync(client, food.Id);
            FirstCost = first.Count;
        }

        for (var i = 0; i < 40; i++) await LogAsync(client, food.Id);

        using var later = _fixture.CountCommands();
        await LogAsync(client, food.Id);

        later.Count.Should().Be(
            FirstCost, "the round trips per log must not depend on how much the user has logged");
    }

    private int FirstCost;

    private async Task<(HttpClient Client, Guid UserId)> UserAsync()
    {
        var id = Guid.NewGuid();
        var email = $"budget-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email);
        return (_fixture.CreateAuthenticatedClient(id, email), id);
    }

    private static async Task LogAsync(HttpClient client, Guid foodId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/Meals", new { foodId, mealType = "SNACK", servings = 1 });
        response.EnsureSuccessStatusCode();
    }
}

/// <summary>Counts every command the application issues while it is alive.</summary>
public sealed class CommandCounter : DbCommandInterceptor
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public bool Enabled { get; set; } = true;

    public override DbCommand CommandCreated(CommandEndEventData eventData, DbCommand result)
    {
        if (Enabled) Interlocked.Increment(ref _count);
        return base.CommandCreated(eventData, result);
    }

    public void Reset() => Interlocked.Exchange(ref _count, 0);
}
