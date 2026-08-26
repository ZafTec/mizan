using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

/// <summary>
/// The ceilings are what stand between a loop and a surprise invoice, so the
/// tests are about what happens when they are reached. Fixture limits are
/// deliberately tiny - free is 3 requests / 1000 tokens, global is 4000.
/// </summary>
namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public class AiQuotaServiceTests
{
    // The global ceiling is shared state by design, so each test starts from an
    // empty ledger or it would be measuring the test before it.
    private const string Feature = "chat";

    private readonly ApiTestFixture _fixture;

    public AiQuotaServiceTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AReservationCountsBeforeTheCallReturns()
    {
        await _fixture.ResetDatabaseAsync();
        var user = await UserAsync();

        await ReserveAsync(user, 100);

        var snapshot = await SnapshotAsync(user);
        snapshot.RequestsUsed.Should().Be(1);
        snapshot.TokensUsed.Should().Be(100);
    }

    [Fact]
    public async Task TheRequestCapStopsTheFourthCall()
    {
        await _fixture.ResetDatabaseAsync();
        var user = await UserAsync();

        for (var i = 0; i < 3; i++) await ReserveAsync(user, 10);

        var refused = await Refused(() => ReserveAsync(user, 10));
        refused.Scope.Should().Be(AiQuotaScope.User);
    }

    [Fact]
    public async Task TheTokenBudgetStopsACallThatWouldExceedIt()
    {
        await _fixture.ResetDatabaseAsync();
        var user = await UserAsync();

        await ReserveAsync(user, 900);

        var refused = await Refused(() => ReserveAsync(user, 200));
        refused.Scope.Should().Be(AiQuotaScope.User);
    }

    /// <summary>
    /// The one that stops a loop or an abusive account from spending everyone
    /// else's headroom. It trips for a user who is well inside their own cap.
    /// </summary>
    [Fact]
    public async Task TheGlobalCeilingStopsEveryone()
    {
        await _fixture.ResetDatabaseAsync();
        // Four users at 900 tokens each: 3600 of the 4000-token global ceiling,
        // and every one of them well inside the free cap of 3 requests / 1000
        // tokens.
        for (var i = 0; i < 4; i++)
        {
            var spender = await UserAsync();
            await ReserveAsync(spender, 900);
        }

        // This one has spent nothing at all and is still refused, because the
        // ceiling is not theirs - which is why the message is different.
        var innocent = await UserAsync();
        var refused = await Refused(() => ReserveAsync(innocent, 500));

        refused.Scope.Should().Be(AiQuotaScope.Global);
        refused.Message.Should().Contain("capacity");
    }

    [Fact]
    public async Task SettlingReplacesTheEstimateWithTheTruth()
    {
        await _fixture.ResetDatabaseAsync();
        var user = await UserAsync();
        var lease = await ReserveAsync(user, 500);

        await SettleAsync(lease, new AiTokenUsage(120, 40), AiCallOutcome.Succeeded);

        var row = await RowAsync(lease.Id);
        row.PromptTokens.Should().Be(120);
        row.CompletionTokens.Should().Be(40);
        row.Outcome.Should().Be(AiCallOutcome.Succeeded);

        (await SnapshotAsync(user)).TokensUsed.Should().Be(160);
    }

    /// <summary>A settle that runs twice must not bill twice.</summary>
    [Fact]
    public async Task SettlingTwiceDoesNotDoubleCount()
    {
        await _fixture.ResetDatabaseAsync();
        var user = await UserAsync();
        var lease = await ReserveAsync(user, 500);

        await SettleAsync(lease, new AiTokenUsage(100, 0), AiCallOutcome.Succeeded);
        await SettleAsync(lease, new AiTokenUsage(999, 999), AiCallOutcome.Succeeded);

        (await RowAsync(lease.Id)).PromptTokens.Should().Be(100);
    }

    /// <summary>
    /// A call that failed still consumed tokens at the provider, so it still
    /// counts. Otherwise a failing loop is free.
    /// </summary>
    [Fact]
    public async Task AFailedCallStillCounts()
    {
        await _fixture.ResetDatabaseAsync();
        var user = await UserAsync();
        var lease = await ReserveAsync(user, 300);

        await SettleAsync(lease, new AiTokenUsage(250, 0), AiCallOutcome.ProviderError);

        var snapshot = await SnapshotAsync(user);
        snapshot.RequestsUsed.Should().Be(1);
        snapshot.TokensUsed.Should().Be(250);
    }

    private async Task<Guid> UserAsync()
    {
        var id = Guid.NewGuid();
        await _fixture.SeedUserAsync(id, $"quota-{id:N}@example.com", emailVerified: true);
        return id;
    }

    private async Task<AiQuotaLease> ReserveAsync(Guid userId, int tokens)
    {
        using var scope = _fixture.Services.CreateScope();
        var quota = scope.ServiceProvider.GetRequiredService<IAiQuotaService>();
        return await quota.ReserveAsync(userId, null, Feature, tokens);
    }

    private async Task SettleAsync(AiQuotaLease lease, AiTokenUsage usage, AiCallOutcome outcome)
    {
        using var scope = _fixture.Services.CreateScope();
        var quota = scope.ServiceProvider.GetRequiredService<IAiQuotaService>();
        await quota.SettleAsync(lease, usage, "test-model", 42, outcome);
    }

    private async Task<AiQuotaSnapshot> SnapshotAsync(Guid userId)
    {
        using var scope = _fixture.Services.CreateScope();
        var quota = scope.ServiceProvider.GetRequiredService<IAiQuotaService>();
        return await quota.GetUserSnapshotAsync(userId);
    }

    private async Task<AiUsageLog> RowAsync(Guid id)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        return await db.AiUsageLogs.AsNoTracking().FirstAsync(log => log.Id == id);
    }

    private static async Task<AiQuotaExceededException> Refused(Func<Task> action)
    {
        var thrown = await Record.ExceptionAsync(action);
        thrown.Should().BeOfType<AiQuotaExceededException>();
        return (AiQuotaExceededException)thrown!;
    }
}
