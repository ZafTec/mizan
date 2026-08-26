using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Ai;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The publish path, end to end. The interesting property is the refusal: a
/// draft nobody has proven must not reach production however the request is
/// made (docs/REFOCUS.md §12).
/// </summary>
[Collection("ApiIntegration")]
public class AiPromptPlatformTests
{
    private readonly ApiTestFixture _fixture;

    public AiPromptPlatformTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task TheConsoleListsEverySurfaceCodeCanAskFor()
    {
        await _fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();

        var prompts = await client.GetFromJsonAsync<List<AiPromptSummaryDto>>("/api/Admin/Ai/Prompts");

        prompts!.Select(p => p.Key).Should().Contain([AiPromptKeys.Chat, AiPromptKeys.FoodAnalysis]);
        prompts.Should().OnlyContain(p => p.PublishedVersion == null);
    }

    [Fact]
    public async Task AnUnprovenDraftIsRefusedAtPublish()
    {
        await _fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();

        var draft = await CreateDraftAsync(client, "Be concise and cite the log.");

        var response = await client.PostAsync($"/api/Admin/Ai/Prompts/versions/{draft.Id}/publish", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("adversarial");
        (await PublishedVersionAsync(AiPromptKeys.Chat)).Should().BeNull();
    }

    [Fact]
    public async Task TheMatrixExplainsWhatIsStillMissing()
    {
        await _fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();
        var draft = await CreateDraftAsync(client, "Be concise.");

        var matrix = await client.GetFromJsonAsync<AiEvalMatrixDto>(
            $"/api/Admin/Ai/Prompts/versions/{draft.Id}/evals");

        matrix!.Publishable.Should().BeFalse();
        matrix.Cases.Should().NotBeEmpty("the synthetic suite ships with the schema");
        matrix.Cases.Should().Contain(c => c.IsAdversarial);
        matrix.Runs.Should().BeEmpty();
        matrix.BlockedReason.Should().NotBeNull();
    }

    [Fact]
    public async Task APassedSuitePublishesAndArchivesTheIncumbent()
    {
        await _fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();

        var first = await CreateDraftAsync(client, "Version one.");
        await PassEveryAdversarialCaseAsync(first.Id);
        (await client.PostAsync($"/api/Admin/Ai/Prompts/versions/{first.Id}/publish", null))
            .EnsureSuccessStatusCode();

        var second = await CreateDraftAsync(client, "Version two.");
        await PassEveryAdversarialCaseAsync(second.Id);
        (await client.PostAsync($"/api/Admin/Ai/Prompts/versions/{second.Id}/publish", null))
            .EnsureSuccessStatusCode();

        (await PublishedVersionAsync(AiPromptKeys.Chat)).Should().Be(second.Id);
        (await StatusAsync(first.Id)).Should().Be(AiPromptStatus.Archived);
    }

    /// <summary>
    /// Rolling back is publishing something that already cleared the gate, so
    /// it must not demand a fresh suite run - that friction is exactly what
    /// stops people rolling back when production is misbehaving.
    /// </summary>
    [Fact]
    public async Task AnArchivedVersionRollsBackWithoutRerunningEvals()
    {
        await _fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();

        var first = await CreateDraftAsync(client, "Version one.");
        await PassEveryAdversarialCaseAsync(first.Id);
        (await client.PostAsync($"/api/Admin/Ai/Prompts/versions/{first.Id}/publish", null))
            .EnsureSuccessStatusCode();

        var second = await CreateDraftAsync(client, "Version two.");
        await PassEveryAdversarialCaseAsync(second.Id);
        (await client.PostAsync($"/api/Admin/Ai/Prompts/versions/{second.Id}/publish", null))
            .EnsureSuccessStatusCode();

        // Discarding what version one proved, so a gate check would now refuse it.
        await ClearRunsAsync(first.Id);

        (await client.PostAsync($"/api/Admin/Ai/Prompts/versions/{first.Id}/publish", null))
            .EnsureSuccessStatusCode();

        (await PublishedVersionAsync(AiPromptKeys.Chat)).Should().Be(first.Id);
    }

    [Fact]
    public async Task EditingADraftDiscardsWhatTheOldTextProved()
    {
        await _fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();
        var draft = await CreateDraftAsync(client, "Version one.");
        await PassEveryAdversarialCaseAsync(draft.Id);

        var edited = await client.PutAsJsonAsync(
            $"/api/Admin/Ai/Prompts/versions/{draft.Id}",
            new { Body = "Something else entirely.", SoftPolicy = "{}", Notes = (string?)null });
        edited.EnsureSuccessStatusCode();

        var matrix = await client.GetFromJsonAsync<AiEvalMatrixDto>(
            $"/api/Admin/Ai/Prompts/versions/{draft.Id}/evals");
        matrix!.Runs.Should().BeEmpty();
        matrix.Publishable.Should().BeFalse();
    }

    [Fact]
    public async Task APublishedVersionIsRejectedForEditing()
    {
        await _fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();
        var draft = await CreateDraftAsync(client, "Version one.");
        await PassEveryAdversarialCaseAsync(draft.Id);
        (await client.PostAsync($"/api/Admin/Ai/Prompts/versions/{draft.Id}/publish", null))
            .EnsureSuccessStatusCode();

        var response = await client.PutAsJsonAsync(
            $"/api/Admin/Ai/Prompts/versions/{draft.Id}",
            new { Body = "Rewriting history.", SoftPolicy = "{}", Notes = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AnOrdinaryUserCannotReachTheConsole()
    {
        var userId = Guid.NewGuid();
        await _fixture.SeedUserAsync(userId, $"prompt-{userId:N}@example.com");
        using var client = _fixture.CreateAuthenticatedClient(userId, $"prompt-{userId:N}@example.com");

        (await client.GetAsync("/api/Admin/Ai/Prompts")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WithNothingPublishedTheResolverFallsBackToTheBuiltInDefault()
    {
        await _fixture.ResetDatabaseAsync();

        var resolved = await ResolveAsync(AiPromptKeys.Chat);

        resolved.VersionId.Should().BeNull();
        resolved.SystemPrompt.Should().Contain(AiHardConstraints.Preamble);
        resolved.SystemPrompt.Should().Contain(AiPromptDefaults.Body(AiPromptKeys.Chat));
    }

    [Fact]
    public async Task ThePublishedBodyAndItsSoftPolicyBothReachTheModel()
    {
        await _fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();

        var draft = await CreateDraftAsync(
            client,
            "Answer in metric units.",
            """{"tone":"dry","refusalTopics":["supplement dosing"]}""");
        await PassEveryAdversarialCaseAsync(draft.Id);
        (await client.PostAsync($"/api/Admin/Ai/Prompts/versions/{draft.Id}/publish", null))
            .EnsureSuccessStatusCode();

        var resolved = await ResolveAsync(AiPromptKeys.Chat);

        resolved.VersionId.Should().Be(draft.Id);
        resolved.SystemPrompt.Should().Contain("Answer in metric units.");
        resolved.SystemPrompt.Should().Contain("dry");
        resolved.SystemPrompt.Should().Contain("supplement dosing");
        // The hard half is always first, whatever the editable half says.
        resolved.SystemPrompt.Should().StartWith(AiHardConstraints.Preamble);
    }

    [Fact]
    public async Task AMalformedSoftPolicyIsRefusedOnSaveRatherThanAtRuntime()
    {
        await _fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/Admin/Ai/Prompts/{AiPromptKeys.Chat}/drafts",
            new { Body = "Fine.", SoftPolicy = "{not json", Notes = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<HttpClient> AdminAsync()
    {
        var id = Guid.NewGuid();
        var email = $"prompt-admin-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email, role: "admin");
        return _fixture.CreateAuthenticatedClient(id, email, "admin");
    }

    private static async Task<AiPromptVersionDto> CreateDraftAsync(
        HttpClient client, string body, string softPolicy = "{}")
    {
        var response = await client.PostAsJsonAsync(
            $"/api/Admin/Ai/Prompts/{AiPromptKeys.Chat}/drafts",
            new { Body = body, SoftPolicy = softPolicy, Notes = (string?)null });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AiPromptVersionDto>())!;
    }

    /// <summary>
    /// Writes the passing runs the gate looks for. The runner itself needs a
    /// provider, and what is under test here is the gate, not the provider.
    /// </summary>
    private async Task PassEveryAdversarialCaseAsync(Guid versionId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

        var cases = await db.AiEvalCases.AsNoTracking()
            .Where(c => c.PromptKey == AiPromptKeys.Chat)
            .Select(c => c.Id)
            .ToListAsync();

        db.AiEvalRuns.AddRange(cases.Select(caseId => new AiEvalRun
        {
            Id = Guid.CreateVersion7(),
            VersionId = versionId,
            CaseId = caseId,
            Outcome = AiEvalOutcome.Passed,
            SchemaValid = true,
            PromptTokens = 100,
            CompletionTokens = 20,
            CostMicros = 30,
            LatencyMs = 250,
            CreatedAt = DateTime.UtcNow,
        }));

        await db.SaveChangesAsync();
    }

    private async Task ClearRunsAsync(Guid versionId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        await db.AiEvalRuns.Where(r => r.VersionId == versionId).ExecuteDeleteAsync();
    }

    private async Task<ResolvedPrompt> ResolveAsync(string key)
    {
        using var scope = _fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAiPromptResolver>().ResolveAsync(key);
    }

    private async Task<Guid?> PublishedVersionAsync(string key)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        return await db.AiPromptVersions.AsNoTracking()
            .Where(v => v.Prompt!.Key == key && v.Status == AiPromptStatus.Published)
            .Select(v => (Guid?)v.Id)
            .FirstOrDefaultAsync();
    }

    private async Task<AiPromptStatus> StatusAsync(Guid versionId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        return await db.AiPromptVersions.AsNoTracking()
            .Where(v => v.Id == versionId).Select(v => v.Status).FirstAsync();
    }
}
