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
/// Chat is persisted now, so the properties worth testing are the ones a blob
/// of thread state could not give you: a turn traces to the version that
/// produced it, a failed call leaves nothing behind, and a thread belongs to
/// exactly one person (docs/REFOCUS.md §12).
/// </summary>
[Collection("ApiIntegration")]
public class AiChatTests
{
    private readonly ApiTestFixture _fixture;

    public AiChatTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AFirstMessageOpensAThreadTitledAfterIt()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await UserAsync();
        _fixture.Ai.Reply("About 96 g so far.");

        var turn = await SendAsync(client, null, "How much protein have I logged today?");

        turn.ThreadId.Should().NotBeEmpty();
        turn.Title.Should().Be("How much protein have I logged today?");
        turn.Reply.Content.Should().Be("About 96 g so far.");
        turn.Reply.FromUser.Should().BeFalse();
    }

    [Fact]
    public async Task ALongOpeningQuestionIsTrimmedToATitleThatFitsAList()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await UserAsync();
        var question = string.Join(" ", Enumerable.Repeat("protein", 30));

        var turn = await SendAsync(client, null, question);

        turn.Title.Length.Should().BeLessThanOrEqualTo(60);
        turn.Title.Should().EndWith("…");
    }

    [Fact]
    public async Task BothSidesOfTheTurnArePersistedInOrder()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await UserAsync();
        _fixture.Ai.Reply("First answer.");
        var turn = await SendAsync(client, null, "First question?");

        _fixture.Ai.Reply("Second answer.");
        await SendAsync(client, turn.ThreadId, "Second question?");

        var thread = await client.GetFromJsonAsync<AiChatThreadDetailDto>(
            $"/api/Ai/threads/{turn.ThreadId}");

        thread!.Messages.Select(m => m.Content).Should().Equal(
            "First question?", "First answer.", "Second question?", "Second answer.");
        thread.Messages.Select(m => m.FromUser).Should().Equal(true, false, true, false);
    }

    /// <summary>
    /// The whole point of persisting the thread: the second call carries the
    /// first exchange, so the model is answering a conversation rather than a
    /// sentence.
    /// </summary>
    [Fact]
    public async Task TheSecondTurnReplaysTheFirst()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await UserAsync();
        _fixture.Ai.Reply("Chicken and rice.");
        var turn = await SendAsync(client, null, "What did I eat?");

        _fixture.Ai.Reply("Roughly 700 kcal.");
        await SendAsync(client, turn.ThreadId, "How many calories was that?");

        var sent = _fixture.Ai.LastCall.Messages.Select(m => m.Content).ToList();
        sent.Should().Contain("What did I eat?");
        sent.Should().Contain("Chicken and rice.");
        sent.Should().Contain("How many calories was that?");
    }

    [Fact]
    public async Task TheAnswerRecordsWhichPromptVersionProducedIt()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await UserAsync();
        var turn = await SendAsync(client, null, "Anything?");

        var stored = await MessagesAsync(turn.ThreadId);
        var reply = stored.Single(m => m.Role == AiChatRole.Assistant);

        // Nothing is published in this fixture, so the built-in default
        // answered - which is exactly what a null version id means.
        reply.PromptVersionId.Should().BeNull();

        var ledger = await LedgerAsync();
        ledger.Should().Contain(log => log.Feature == AiFeatures.Chat);
    }

    /// <summary>A failed call must not leave half a turn in the transcript.</summary>
    [Fact]
    public async Task AFailedCallPersistsNothing()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await UserAsync();
        _fixture.Ai.Reply("Fine.");
        var turn = await SendAsync(client, null, "First question?");

        _fixture.Ai.Fail("The assistant could not be reached. Try again shortly.");
        var response = await client.PostAsJsonAsync(
            "/api/Ai/chat", new { threadId = turn.ThreadId, message = "Second question?" });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var stored = await MessagesAsync(turn.ThreadId);
        stored.Should().HaveCount(2);
        stored.Should().NotContain(m => m.Content == "Second question?");
    }

    [Fact]
    public async Task AnotherPersonsThreadIsNotFound()
    {
        await _fixture.ResetDatabaseAsync();
        var (mine, _) = await UserAsync();
        var turn = await SendAsync(mine, null, "Private question?");

        var (theirs, _) = await UserAsync();

        (await theirs.GetAsync($"/api/Ai/threads/{turn.ThreadId}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await theirs.DeleteAsync($"/api/Ai/threads/{turn.ThreadId}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ContinuingSomeoneElsesThreadIsNotFound()
    {
        await _fixture.ResetDatabaseAsync();
        var (mine, _) = await UserAsync();
        var turn = await SendAsync(mine, null, "Private question?");

        var (theirs, _) = await UserAsync();
        var response = await theirs.PostAsJsonAsync(
            "/api/Ai/chat", new { threadId = turn.ThreadId, message = "Carrying on." });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletingAThreadTakesItsMessagesWithIt()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await UserAsync();
        var turn = await SendAsync(client, null, "Forget this.");

        (await client.DeleteAsync($"/api/Ai/threads/{turn.ThreadId}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        (await MessagesAsync(turn.ThreadId)).Should().BeEmpty();
    }

    [Fact]
    public async Task ThreadsAreListedMostRecentlyUsedFirst()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await UserAsync();
        var first = await SendAsync(client, null, "Older question?");
        var second = await SendAsync(client, null, "Newer question?");
        await SendAsync(client, first.ThreadId, "Reviving the older one.");

        var threads = await client.GetFromJsonAsync<List<AiChatThreadDto>>("/api/Ai/threads");

        threads!.Select(t => t.Id).Should().StartWith([first.ThreadId, second.ThreadId]);
    }

    [Fact]
    public async Task AnEmptyMessageIsRejectedBeforeItCostsAnything()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await UserAsync();
        var before = _fixture.Ai.Calls.Count;

        var response = await client.PostAsJsonAsync(
            "/api/Ai/chat", new { threadId = (Guid?)null, message = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _fixture.Ai.Calls.Count.Should().Be(before);
    }

    private async Task<(HttpClient Client, Guid UserId)> UserAsync()
    {
        _fixture.Ai.Reset();
        var id = Guid.NewGuid();
        var email = $"chat-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email);
        return (_fixture.CreateAuthenticatedClient(id, email), id);
    }

    private static async Task<AiChatTurnDto> SendAsync(HttpClient client, Guid? threadId, string message)
    {
        var response = await client.PostAsJsonAsync("/api/Ai/chat", new { threadId, message });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AiChatTurnDto>())!;
    }

    private async Task<List<AiChatMessage>> MessagesAsync(Guid threadId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        return await db.AiChatMessages.AsNoTracking()
            .Where(m => m.ThreadId == threadId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    private async Task<List<AiUsageLog>> LedgerAsync()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        return await db.AiUsageLogs.AsNoTracking().ToListAsync();
    }
}
