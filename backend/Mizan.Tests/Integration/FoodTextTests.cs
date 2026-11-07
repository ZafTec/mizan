using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Ai;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Nutrition;
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
    public async Task EachFood_ComesWithItsNutritionPer100g()
    {
        var (client, _) = await ProUserAsync();
        _fixture.Ai.Reply(Analysis);

        var response = await Post(client, "two eggs and a slice of toast with butter");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var toast = json.GetProperty("foods")[1].GetProperty("per100g");
        toast.GetProperty("calories").GetDecimal().Should().Be(316.67m);
        toast.GetProperty("proteinGrams").GetDecimal().Should().Be(6.67m);
        toast.GetProperty("carbsGrams").GetDecimal().Should().Be(36.67m);
        toast.GetProperty("fatGrams").GetDecimal().Should().Be(15m);
    }

    /// <summary>M-11: a food the catalogue knows comes back as that food, with the catalogue's numbers.</summary>
    [Fact]
    public async Task AFoodTheCatalogueKnows_ComesBackWithItsIdAndTheCataloguesNutrition()
    {
        var (client, _) = await ProUserAsync();
        var injera = await _fixture.SeedFoodAsync("Injera", 166, 4.5m, 34, 1.1m);
        _fixture.Ai.Reply("""
            {"foods":[
               {"name":"injera","portionGrams":200,"calories":320,"protein":7,"carbs":70,"fat":2},
               {"name":"Spiced unicorn stew","portionGrams":100,"calories":150,"protein":10,"carbs":5,"fat":9}],
             "totalCalories":470,"confidence":0.6}
            """);

        var result = await (await Post(client, "two pieces of injera with stew")).Content.ReadFromJsonAsync<FoodAnalysisResult>();

        var bread = result!.Foods[0];
        (bread.FoodId, bread.Name, bread.Source).Should().Be((injera.Id, "Injera", "catalogue"));
        (bread.Calories, bread.Protein, bread.Carbs, bread.Fat).Should().Be((332m, 9m, 68m, 2.2m));
        bread.Per100g!.ProteinGrams.Should().Be(4.5m);
        var stew = result.Foods[1];
        (stew.FoodId, stew.Source, stew.Protein).Should().Be(((Guid?)null, "estimate", 10m));
        result.TotalCalories.Should().Be(482);
    }

    /// <summary>
    /// M-11 retest: "egg" and "misir wot" came back as estimates although the catalogue has Egg and
    /// the person has a Misir wot recipe. A plural or another spelling still names the food, and the
    /// person's own recipe comes before a catalogue food of the same name, with its id and kind.
    /// </summary>
    [Fact]
    public async Task APluralOrAnotherSpelling_FindsTheFood_AndTheirOwnRecipeComesFirst()
    {
        var (client, user) = await ProUserAsync();
        var world = new PlanningWorld(_fixture);
        var egg = await _fixture.SeedFoodAsync("Egg", 143, 12.6m, 0.7m, 9.5m);
        await _fixture.SeedFoodAsync("Eggplant", 25, 1, 6, 0.2m);
        await _fixture.SeedFoodAsync("Misir wot", 150, 8, 18, 5);
        var lentils = await _fixture.SeedFoodAsync("Lentils", 116, 9, 20, 0.4m);
        // Four servings of 200 g lentils: 232 kcal, P 18 g, C 40 g, F 0.8 g a serving.
        var misir = await world.RecipeAsync(user, "Misir wot", 4, isPublic: false, (lentils, 800m, "g", null));
        _fixture.Ai.Reply("""
            {"foods":[
               {"name":"eggs","portionGrams":100,"calories":150,"protein":12,"carbs":1,"fat":10},
               {"name":"misir wat","portionGrams":250,"calories":300,"protein":15,"carbs":40,"fat":8}],
             "totalCalories":450,"confidence":0.6}
            """);

        var result = await (await Post(client, "two eggs and misir wat")).Content.ReadFromJsonAsync<FoodAnalysisResult>();

        var eggs = result!.Foods[0];
        (eggs.FoodId, eggs.RecipeId, eggs.Name, eggs.Source, eggs.Calories).Should().Be((egg.Id, (Guid?)null, "Egg", "catalogue", 143m));
        var stew = result.Foods[1];
        (stew.RecipeId, stew.FoodId, stew.Name, stew.Source, stew.Servings).Should().Be((misir.Id, (Guid?)null, "Misir wot", "recipe", 1m));
        (stew.Calories, stew.Protein, stew.Carbs, stew.Fat).Should().Be((232m, 18m, 40m, 0.8m));
        result.TotalCalories.Should().Be(375);
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
