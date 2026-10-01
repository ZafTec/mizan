using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Ai;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// A sentence becomes a proposal the way a photo does: structured foods, nothing written to the diary, metered like
/// every other model call, and out of reach of a free plan.
/// </summary>
[Collection("ApiIntegration")]
public class FoodTextTests
{
    private const string Analysis = """
        {"foods":[
           {"name":"Scrambled eggs","portionGrams":120,"calories":180,"protein":12,"carbs":2,"fat":14},
           {"name":"Toast with butter","portionGrams":60,"calories":190,"protein":4,"carbs":22,"fat":9}],
         "totalCalories":370,"confidence":0.7,"note":"Assumed two eggs and one slice."}
        """;

    private readonly ApiTestFixture _fixture;

    public FoodTextTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ASentence_ComesBackAsStructuredFoods()
    {
        var (client, _) = await ProUserAsync();
        _fixture.Ai.Reply(Analysis);

        var response = await Post(client, "two eggs and a slice of toast with butter");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<FoodAnalysisResult>();
        result!.Foods.Select(f => f.Name).Should().Equal("Scrambled eggs", "Toast with butter");
        result.TotalCalories.Should().Be(370);
        result.Note.Should().Contain("Assumed");
    }

    [Fact]
    public async Task TheModelIsToldItIsReadingWords_AndSeesTheDescription()
    {
        var (client, _) = await ProUserAsync();
        _fixture.Ai.Reply(Analysis);

        await Post(client, "a bowl of rice with chicken");

        var request = _fixture.Ai.LastCall;
        request.Messages.Should().Contain(m => m.Content.Contains("There is no photo"));
        request.Messages.Should().Contain(m => m.Content == "a bowl of rice with chicken");
        request.Messages.Should().NotContain(m => m.Image != null);
    }

    [Fact]
    public async Task NothingIsWrittenToTheDiary()
    {
        var (client, user) = await ProUserAsync();
        _fixture.Ai.Reply(Analysis);

        await Post(client, "two eggs");

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        (await db.FoodDiaryEntries.CountAsync(e => e.UserId == user)).Should().Be(0);
    }

    [Fact]
    public async Task AFailedReading_StillLandsInTheLedger_AndIsNotScrapedFromProse()
    {
        var (client, user) = await ProUserAsync();
        _fixture.Ai.Reply("Probably about 400 calories.");

        var response = await Post(client, "something");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        (await db.AiUsageLogs.AsNoTracking().Where(l => l.UserId == user).ToListAsync())
            .Should().ContainSingle(l => l.Feature == AiFeatures.FoodAnalysis);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyDescription_IsRefusedBeforeItCostsAnything(string text)
    {
        var (client, _) = await ProUserAsync();
        var before = _fixture.Ai.Calls.Count;

        var response = await Post(client, text);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("errorCode").GetString().Should().Be("no_description");
        _fixture.Ai.Calls.Count.Should().Be(before);
    }

    [Fact]
    public async Task ATooLongDescription_IsRefusedBeforeItCostsAnything()
    {
        var (client, _) = await ProUserAsync();
        var before = _fixture.Ai.Calls.Count;

        var response = await Post(client, new string('x', 601));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _fixture.Ai.Calls.Count.Should().Be(before);
    }

    [Fact]
    public async Task FreeUsersAreWalled()
    {
        await _fixture.ResetDatabaseAsync();
        var id = Guid.NewGuid();
        var email = $"text-free-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email);
        using var client = _fixture.CreateAuthenticatedClient(id, email);

        (await Post(client, "two eggs")).StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
    }

    [Fact]
    public async Task ItNeedsASignedInUser()
    {
        using var anonymous = _fixture.CreateClient();

        (await Post(anonymous, "two eggs")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<(HttpClient Client, Guid User)> ProUserAsync()
    {
        await _fixture.ResetDatabaseAsync();
        _fixture.Ai.Reset();
        var id = Guid.NewGuid();
        var email = $"text-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email);
        await _fixture.GrantProAsync(id);
        return (_fixture.CreateAuthenticatedClient(id, email), id);
    }

    private static Task<HttpResponseMessage> Post(HttpClient client, string description) =>
        client.PostAsJsonAsync("/api/Nutrition/ai/analyze-text", new { description });
}
