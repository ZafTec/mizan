using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Ai;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Ai;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// Chat is persisted now, so the properties worth testing are the ones a blob
/// of thread state could not give you: a turn traces to the version that
/// produced it, a failed call leaves nothing behind, and a thread belongs to
/// exactly one person (docs/AI.md).
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
        // The stored question comes back too, so a client can swap its optimistic line for the record.
        turn.Message.FromUser.Should().BeTrue();
        turn.Message.Content.Should().Be("How much protein have I logged today?");
        turn.Message.ImageUrl.Should().BeNull();
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

    /// <summary>
    /// #119: a plan write while writes are off is a typed refusal naming the switch, not a failed
    /// turn. Planning has no switch of its own, so the refusal names nutrition.
    /// </summary>
    [Fact]
    public async Task AWriteWithoutWriteConsentIsRefusedNamingTheAxis()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync();
        await _fixture.GrantAiConsentAsync(userId, read: true, write: false);
        _fixture.Ai.CallTools((
            "add_plan_entry",
            $$"""{"planId":"{{Guid.NewGuid()}}","dayIndex":0,"slot":"Breakfast","kind":"text","text":"Eggs"}"""));
        _fixture.Ai.Reply("Adding to your plan is switched off.");

        var turn = await SendAsync(client, null, "Add eggs to tomorrow's breakfast");

        turn.Reply.Content.Should().Be("Adding to your plan is switched off.");
        turn.Performed.Should().ContainSingle().Which.Succeeded.Should().BeFalse();
        turn.ConsentRequired.Should().Equal(
            new AiConsentRequirement(AiErrorCodes.ConsentRequired, "nutrition", "write"));
    }

    /// <summary>
    /// A client reports where the person is (docs/AI.md#app-context): the fact waits for their next
    /// message, goes to the model with it, and stays in the thread for later turns, while the person's
    /// own view of the thread shows only what was said.
    /// </summary>
    [Fact]
    public async Task AppContextAClientReports_ReachesTheModelWithTheNextMessage_AndStaysForLaterTurns()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await UserAsync();
        const string step = "The person is setting up a reminder. Chosen so far: Log a meal (kind meal_log). The chat is now asking \"Which days?\".";

        var noted = await client.PostAsJsonAsync("/api/Ai/conversation/context", new AiContextNoteRequest(step, 15));
        noted.EnsureSuccessStatusCode();
        var waiting = await client.GetFromJsonAsync<AiConversationDto>("/api/Ai/conversation");
        waiting!.PendingContext.Should().Be(step);
        waiting.ThreadId.Should().NotBeNull();

        _fixture.Ai.Reply("Which day of the week?");
        var response = await client.PostAsJsonAsync("/api/Ai/chat", new AiChatRequest(waiting.ThreadId, "once a week", waiting.PendingContext));
        response.EnsureSuccessStatusCode();
        var turn = (await response.Content.ReadFromJsonAsync<AiChatTurnDto>())!;

        turn.ThreadId.Should().Be(waiting.ThreadId!.Value);
        turn.Title.Should().Be("once a week");
        ContextSent().Should().ContainSingle().Which.Should().EndWith(step);
        (await client.GetFromJsonAsync<AiConversationDto>("/api/Ai/conversation"))!.PendingContext.Should().BeNull();

        _fixture.Ai.Reply("Set for Sundays.");
        await SendAsync(client, turn.ThreadId, "sunday");
        ContextSent().Should().ContainSingle().Which.Should().EndWith(step);

        var thread = await client.GetFromJsonAsync<AiChatThreadDetailDto>($"/api/Ai/threads/{turn.ThreadId}");
        thread!.Messages.Select(m => m.Content).Should().Equal("once a week", "Which day of the week?", "sunday", "Set for Sundays.");
    }

    [Fact]
    public async Task AfterAQuietGap_TheNextMessageStartsANewConversation_AndWaitingContextGoesWithIt()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, _) = await UserAsync();
        var start = DateTimeOffset.UtcNow;
        _fixture.Clock.Set(start);
        var earlier = await SendAsync(client, null, "How much protein today?");
        (await client.GetFromJsonAsync<AiConversationDto>("/api/Ai/conversation"))!.ThreadId.Should().Be(earlier.ThreadId);

        _fixture.Clock.Set(start.AddHours(4));
        (await client.GetFromJsonAsync<AiConversationDto>("/api/Ai/conversation"))!.ThreadId.Should().BeNull();

        (await client.PostAsJsonAsync("/api/Ai/conversation/context", new AiContextNoteRequest("Asking: What time?", 15))).EnsureSuccessStatusCode();
        var current = await client.GetFromJsonAsync<AiConversationDto>("/api/Ai/conversation");
        current!.ThreadId.Should().NotBeNull().And.NotBe(earlier.ThreadId);
        current.PendingContext.Should().Be("Asking: What time?");

        _fixture.Clock.Set(start.AddHours(4).AddMinutes(20));
        (await client.GetFromJsonAsync<AiConversationDto>("/api/Ai/conversation"))!.PendingContext.Should().BeNull();
    }

    /// <summary>
    /// The live finding: the assistant looked the person's own dish up in the food catalogue, found
    /// nothing and logged an estimate. It now finds the recipe and logs it with the recipe's nutrition.
    /// </summary>
    [Fact]
    public async Task TheAssistantFindsThePersonsRecipe_AndLogsItWithTheRecipesOwnNutrition()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync();
        await _fixture.GrantAiConsentAsync(userId, read: true, write: true);
        var world = new PlanningWorld(_fixture);
        var lentils = await world.FoodAsync("Red lentils", 116, 9, 20, 0.4m);
        var stew = await world.RecipeAsync(userId, "Mesir wot", 2, false, (lentils, 400m, "g", null));
        // Someone else's public recipe is not one of the person's.
        var (other, _) = await world.PersonAsync("other");
        await world.RecipeAsync(other, "Mesir wot deluxe", 2, true, (lentils, 400m, "g", null));

        _fixture.Ai.CallTools(("search_recipes", """{"query":"mesir"}"""));
        _fixture.Ai.CallTools(("log_meal", $$"""{"recipeId":"{{stew.Id}}","servings":1,"mealType":"LUNCH"}"""));
        _fixture.Ai.Reply("Logged a serving of your mesir wot.");

        var turn = await SendAsync(client, null, "I had a bowl of my mesir wot for lunch");

        turn.Performed.Should().HaveCount(2).And.OnlyContain(p => p.Succeeded);
        var lookup = _fixture.Ai.LastCall.Messages.First(m => m.Role == AiRole.Tool).Content;
        lookup.Should().Contain(stew.Id.ToString()).And.Contain("232 kcal").And.NotContain("deluxe");
        var diary = await _fixture.GetFoodDiaryEntriesByUserId(userId);
        diary.Should().ContainSingle().Which.Calories.Should().Be(232m);
    }

    /// <summary>
    /// M-02: asked which of their saved recipes would close the gap, the assistant called
    /// search_recipes with no query and was refused. With no query it now gets every saved recipe:
    /// the person's own and the public ones they favourited, favourites first, with macros per serving.
    /// </summary>
    [Fact]
    public async Task TheAssistantListsThePersonsSavedRecipes_WithoutAQuery()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync();
        await _fixture.GrantAiConsentAsync(userId, read: true, write: false);
        var world = new PlanningWorld(_fixture);
        var lentils = await world.FoodAsync("Red lentils", 116, 9, 20, 0.4m);
        var misir = await world.RecipeAsync(userId, "Misir wot", 2, false, (lentils, 400m, "g", null));
        var (other, _) = await world.PersonAsync("other");
        var shiro = await world.RecipeAsync(other, "Shiro", 2, true, (lentils, 200m, "g", null));
        var stranger = await world.RecipeAsync(other, "Stranger stew", 2, true, (lentils, 200m, "g", null));
        (await client.PostAsync($"/api/Recipes/{shiro.Id}/favorite", null)).IsSuccessStatusCode.Should().BeTrue();

        _fixture.Ai.CallTools(("search_recipes", "{}"));
        _fixture.Ai.Reply("Your misir wot closes most of the gap.");

        var turn = await SendAsync(client, null, "Which of my own saved recipes would best close my gap today?");

        turn.Performed.Should().ContainSingle().Which.Succeeded.Should().BeTrue();
        var listed = _fixture.Ai.LastCall.Messages.First(m => m.Role == AiRole.Tool).Content;
        listed.Should().Contain("Shiro (2 servings, favourite) - per serving 116 kcal")
            .And.Contain($"{misir.Id}").And.Contain("per serving 232 kcal, P18 C40 F0.8")
            .And.NotContain(stranger.Title);
        listed.IndexOf("Shiro", StringComparison.Ordinal).Should().BeLessThan(listed.IndexOf("Misir wot", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MealSuggestionsAreGivenEveryTargetOfTheGoalAndWhatIsLeftToday()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync();
        await _fixture.GrantAiConsentAsync(userId, read: true, write: false);
        (await client.PostAsJsonAsync("/api/Goals", new
        {
            GoalType = "general", TargetCalories = 2000, TargetProteinGrams = 150m, TargetCarbsGrams = 220m, TargetFatGrams = 60m,
        })).StatusCode.Should().Be(HttpStatusCode.Created);
        var food = await _fixture.SeedFoodAsync("Suggestion rice", 100m, 10m, 20m, 5m);
        (await client.PostAsJsonAsync("/api/Nutrition/log", new
        {
            FoodId = food.Id, EntryDate = DateOnly.FromDateTime(DateTime.UtcNow), MealType = "lunch", Servings = 2m,
        })).EnsureSuccessStatusCode();
        _fixture.Ai.Reply("""{"suggestions":[],"note":null}""");

        (await client.PostAsync("/api/Ai/suggestions", null)).EnsureSuccessStatusCode();

        var context = string.Join("\n", _fixture.Ai.LastCall.Messages.Where(m => m.Role == AiRole.System).Select(m => m.Content));
        context.Should().Contain("Targets per day: 2000 kcal, 150 g protein, 220 g carbs, 60 g fat.");
        context.Should().Contain("Today: Left: 1800 kcal, 130 g protein, 180 g carbs, 50 g fat.");
    }

    /// <summary>
    /// M-06: suggestions named meals the person could not make from what Mizan knows. They are now
    /// given the person's recipes and the foods priced in their region, as a plan is.
    /// </summary>
    [Fact]
    public async Task MealSuggestionsAreGivenThePersonsRecipesAndTheFoodsPricedInTheirRegion()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync();
        await _fixture.GrantAiConsentAsync(userId, read: true, write: false);
        var world = new PlanningWorld(_fixture);
        var lentils = await world.FoodAsync("Red lentils", 116, 9, 20, 0.4m);
        await world.RecipeAsync(userId, "Misir wot", 2, false, (lentils, 400m, "g", null));
        var teff = await world.FoodAsync("Teff flour", 367, 13, 73, 2.4m);
        await world.FoodAsync("Unpriced quinoa", 368, 14, 64, 6);
        await world.DbAsync(async db =>
        {
            db.FoodPrices.Add(new FoodPrice
            {
                Id = Guid.NewGuid(), FoodId = teff.Id, RegionCode = Mizan.Domain.Pricing.PriceRegionDefaults.Code,
                Price = 120, Quantity = 1, Unit = Mizan.Domain.Pricing.PriceUnits.Kilogram,
                Confidence = Mizan.Domain.Pricing.PriceConfidence.Observed, ObservedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            return 0;
        });
        _fixture.Ai.Reply("""{"suggestions":[],"note":null}""");

        (await client.PostAsync("/api/Ai/suggestions", null)).EnsureSuccessStatusCode();

        var context = string.Join('\n', _fixture.Ai.LastCall.Messages.Where(m => m.Role == AiRole.System).Select(m => m.Content));
        context.Should().Contain("Misir wot | 2 | 232 kcal")
            .And.Contain("Teff flour | 367 kcal").And.Contain("| 120")
            .And.NotContain("Unpriced quinoa");
    }

    private List<string> ContextSent() =>
        _fixture.Ai.LastCall.Messages
            .Where(m => m.Role == AiRole.System && m.Content.StartsWith("App context", StringComparison.Ordinal))
            .Select(m => m.Content)
            .ToList();

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
