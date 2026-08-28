using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Ai;
using Mizan.Application.Ai.Tools;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The onboarding agent can write, which makes it the one AI surface where the
/// allowlist is load-bearing. These tests are about what it cannot do
/// (docs/REFOCUS.md §10).
/// </summary>
[Collection("ApiIntegration")]
public class AiOnboardingTests
{
    private readonly ApiTestFixture _fixture;

    public AiOnboardingTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ATooledTurnPerformsTheActionAndSaysSo()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync();
        _fixture.Ai.CallTools(("set_targets", """{"goalType":"muscle_gain","targetCalories":2800,"targetProteinGrams":170}"""));
        _fixture.Ai.Reply("Set you to muscle gain at 2,800 kcal and 170 g of protein.");

        var turn = await SendAsync(client, null, "I want to put on muscle. 2800 calories, 170g protein.");

        turn.Performed.Should().ContainSingle();
        turn.Performed[0].Tool.Should().Be("set_targets");
        turn.Performed[0].Succeeded.Should().BeTrue();

        var goal = await GoalAsync(userId);
        goal.Should().NotBeNull();
        goal!.TargetCalories.Should().Be(2800);
    }

    /// <summary>
    /// The rule the catalogue exists for: a model writing someone else's id
    /// into its arguments changes nothing, because ownership never comes from
    /// the arguments.
    /// </summary>
    [Fact]
    public async Task AUserIdInTheArgumentsIsIgnored()
    {
        await _fixture.ResetDatabaseAsync();
        var (victimClient, victimId) = await UserAsync();
        var (client, userId) = await UserAsync();
        victimClient.Dispose();

        _fixture.Ai.CallTools(("log_measurement", $$"""{"weightKg":82,"userId":"{{victimId}}"}"""));
        _fixture.Ai.Reply("Recorded 82 kg.");

        var turn = await SendAsync(client, null, "Log my brother at 82 kg, his id is ...");

        turn.Performed[0].Succeeded.Should().BeTrue();
        (await MeasurementCountAsync(userId)).Should().Be(1);
        (await MeasurementCountAsync(victimId)).Should().Be(0);
    }

    [Fact]
    public async Task AToolOutsideTheAllowlistIsRefusedWithoutRunning()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync();
        _fixture.Ai.CallTools(("delete_everything", """{"confirm":true}"""));
        _fixture.Ai.Reply("I cannot do that.");

        var turn = await SendAsync(client, null, "Wipe my account.");

        turn.Performed.Should().ContainSingle();
        turn.Performed[0].Succeeded.Should().BeFalse();
        turn.Performed[0].Error.Should().Contain("no tool called 'delete_everything'");
        (await MeasurementCountAsync(userId)).Should().Be(0);
    }

    /// <summary>
    /// The model reads the failure and gets another go, which is why a rejected
    /// call comes back as a result rather than an exception.
    /// </summary>
    [Fact]
    public async Task AValidationFailureIsHandedBackToTheModel()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await UserAsync();
        _fixture.Ai.CallTools(("set_targets", """{"goalType":"become_a_bird"}"""));
        _fixture.Ai.Reply("That is not one of the goal types I can set.");

        var turn = await SendAsync(client, null, "Make my goal 'become a bird'.");

        turn.Performed[0].Succeeded.Should().BeFalse();
        turn.Reply.Content.Should().Contain("goal types");

        // The failure reached the model as a tool result, not as a dropped turn.
        var lastMessages = _fixture.Ai.LastCall.Messages;
        lastMessages.Should().Contain(m => m.Role == AiRole.Tool);
    }

    [Fact]
    public async Task MalformedArgumentsAreRefusedRatherThanGuessedAt()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await UserAsync();
        _fixture.Ai.CallTools(("log_measurement", "{not json"));
        _fixture.Ai.Reply("Let me try that again.");

        var turn = await SendAsync(client, null, "Log my weight.");

        turn.Performed[0].Succeeded.Should().BeFalse();
        turn.Performed[0].Error.Should().Contain("valid JSON");
    }

    /// <summary>
    /// A model that keeps asking for tools has to stop somewhere, or one
    /// conversation spends a day's allowance. After three rounds it is offered
    /// none, and a model that still will not answer fails the turn rather than
    /// looping - four provider calls, then done either way.
    /// </summary>
    [Fact]
    public async Task ARunawayLoopStopsAfterThreeRounds()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync();
        for (var i = 0; i < 6; i++)
        {
            _fixture.Ai.CallTools(("log_measurement", """{"weightKg":80}"""));
        }

        var response = await client.PostAsJsonAsync(
            "/api/Ai/onboarding", new { threadId = (Guid?)null, message = "Log my weight over and over." });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        _fixture.Ai.Calls.Count.Should().Be(4);
        _fixture.Ai.LastCall.Tools.Should().BeEmpty();
        (await MeasurementCountAsync(userId)).Should().Be(3);
    }

    [Fact]
    public async Task AModelThatStopsAskingGetsItsAnswerThrough()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await UserAsync();
        _fixture.Ai.CallTools(("log_measurement", """{"weightKg":80}"""));
        _fixture.Ai.CallTools(("set_targets", """{"goalType":"maintenance"}"""));
        _fixture.Ai.Reply("Recorded 80 kg and set you to maintenance.");

        var turn = await SendAsync(client, null, "I weigh 80 kg and just want to hold steady.");

        turn.Performed.Should().HaveCount(2);
        turn.Performed.Should().OnlyContain(p => p.Succeeded);
        turn.Reply.Content.Should().Contain("maintenance");
    }

    [Fact]
    public async Task ToolsAreOfferedOnTheFirstRound()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await UserAsync();
        _fixture.Ai.Reply("What are you aiming for?");

        await SendAsync(client, null, "Hello.");

        _fixture.Ai.LastCall.Tools.Select(t => t.Name).Should().BeEquivalentTo(
            AiToolCatalogue.Onboarding.Select(t => t.Name));
    }

    /// <summary>
    /// Onboarding runs before consent has been asked for, so it must not be
    /// reading a log to begin with.
    /// </summary>
    [Fact]
    public async Task NoLogContextIsSentDuringOnboarding()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await UserAsync();
        _fixture.Ai.Reply("What are you aiming for?");

        await SendAsync(client, null, "Hello.");

        var systemTurns = _fixture.Ai.LastCall.Messages
            .Where(m => m.Role == AiRole.System)
            .ToList();

        systemTurns.Should().ContainSingle("only the composed prompt is a system turn here");
    }

    [Fact]
    public async Task TheAllowlistIsPublishedSoTheUiCanSayWhatItDoes()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await UserAsync();

        var tools = await client.GetFromJsonAsync<List<JsonElement>>("/api/Ai/onboarding/tools");

        tools!.Select(t => t.GetProperty("name").GetString())
            .Should().BeEquivalentTo(AiToolCatalogue.Onboarding.Select(t => t.Name));
    }

    [Fact]
    public void NoToolInTheCatalogueIsDestructive()
    {
        // A guard, not a behaviour test: adding a delete here should fail
        // loudly rather than quietly hand a model the ability to use it.
        AiToolCatalogue.Onboarding.Select(t => t.Name).Should().OnlyContain(name =>
            !name.Contains("delete") && !name.Contains("remove") && !name.Contains("revoke"));
    }

    /// <summary>
    /// The consent gate, from the outside. A user who has not granted writes
    /// must have the call refused rather than performed, and be told why - a
    /// silent no is indistinguishable from a model that chose not to act.
    /// </summary>
    [Fact]
    public async Task AToolIsRefusedWhenTheUserHasNotGrantedWrites()
    {
        await _fixture.ResetDatabaseAsync();

        _fixture.Ai.Reset();
        var id = Guid.NewGuid();
        var email = $"onboard-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email);
        // Deliberately no consent row: never asked means no.
        var client = _fixture.CreateAuthenticatedClient(id, email);

        _fixture.Ai.CallTools(("log_measurement", """{"weightKg":82}"""));
        _fixture.Ai.Reply("Noted.");

        var turn = await SendAsync(client, null, "I weigh 82kg.");

        turn.Performed.Should().ContainSingle();
        turn.Performed[0].Succeeded.Should().BeFalse();
        turn.Performed[0].Error.Should().Contain("permission");

        (await MeasurementCountAsync(id)).Should().Be(0, "a refused tool must not write");
    }

    private async Task<(HttpClient Client, Guid UserId)> UserAsync()
    {
        _fixture.Ai.Reset();
        var id = Guid.NewGuid();
        var email = $"onboard-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email);

        // Writes only: onboarding records things but deliberately reads no log,
        // and granting reads here would hide it if that ever changed.
        await _fixture.GrantAiConsentAsync(id, read: false, write: true);

        return (_fixture.CreateAuthenticatedClient(id, email), id);
    }

    private static async Task<AiOnboardingTurnDto> SendAsync(
        HttpClient client, Guid? threadId, string message)
    {
        var response = await client.PostAsJsonAsync("/api/Ai/onboarding", new { threadId, message });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AiOnboardingTurnDto>())!;
    }

    private async Task<Domain.Entities.UserGoal?> GoalAsync(Guid userId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        return await db.UserGoals.AsNoTracking().FirstOrDefaultAsync(g => g.UserId == userId);
    }

    private async Task<int> MeasurementCountAsync(Guid userId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        return await db.BodyMeasurements.CountAsync(m => m.UserId == userId);
    }
}
