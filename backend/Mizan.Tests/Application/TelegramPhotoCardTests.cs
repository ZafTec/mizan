extern alias telegram;
using System.Net;
using System.Text;
using FluentAssertions;
using Xunit;
using static Mizan.Tests.Application.TelegramFakeWorld;

namespace Mizan.Tests.Application;

/// <summary>
/// A photo estimate that names one of the person's recipes logs that recipe at the servings the API proposed,
/// not a manual meal; a stored photo can be deleted from the card; and /fit shows the API's ranking of the
/// saved recipes with a Log button each. The bot keeps no state: a pressed card's own buttons and link say
/// what is left to do (docs/TELEGRAM.md#commands).
/// </summary>
public class TelegramPhotoCardTests
{
    private static readonly Guid Recipe = Guid.Parse("88888888-8888-4888-8888-888888888888");
    private const string Photo = "https://files.test/meals/abc.jpg";

    private readonly TelegramFakeWorld _world = new();

    private const string Salad = """,{"name":"Salad","source":"estimate","portionGrams":100,"calories":60,"protein":2,"carbs":8,"fat":3}""";

    private static string Analysis(bool withSalad, string? imageUrl = Photo) => $$"""
        {"foods":[
          {"name":"Doro wat","recipeId":"{{Recipe}}","servings":1.5,"source":"recipe","portionGrams":450,"calories":540,"protein":42,"carbs":18,"fat":30}
          {{(withSalad ? Salad : "")}}
         ],
         "totalCalories":{{(withSalad ? 600 : 540)}},"confidence":0.8,"note":null,
         "imageUrl":{{(imageUrl is null ? "null" : $"\"{imageUrl}\"")}}}
        """;

    [Fact]
    public async Task ARecipeOnThePlate_IsLoggedAsTheRecipe_ThenTheRestAsAMeal_AndThePhotoCanBeDeleted()
    {
        _world.AnalysisJson = Analysis(withSalad: true);
        var handler = _world.Handler();

        await handler.HandleAsync(TelegramFakeWorld.Photo(), CancellationToken.None);

        _world.LastText.Should().Contain($"<a href=\"{Photo}\">Your photo</a> is kept for 30 days.");
        var card = _world.LastKeyboard();
        card.Select(b => b.Text).Should().Equal("Log Doro wat, 1.5 servings", "Log the rest", "Split the rest", "Discard", "Delete the photo");
        card.Should().OnlyContain(b => Encoding.UTF8.GetByteCount(b.Data) <= 64);
        _world.MealsPosted.Should().BeEmpty("a proposal is never a silent write");

        // The recipe: its id and the API's servings, no nutrition from the bot.
        await handler.HandleAsync(_world.PressOnLast(card[0].Data), CancellationToken.None);
        var recipe = _world.MealsPosted.Should().ContainSingle().Subject;
        recipe.GetProperty("recipeId").GetGuid().Should().Be(Recipe);
        recipe.GetProperty("servings").GetDecimal().Should().Be(1.5m);
        Absent(recipe, "calories").Should().BeTrue("the recipe's nutrition is the API's");
        _world.LastText.Should().StartWith("Logged Doro wat, 1.5 servings.");
        _world.LastKeyboard().Select(b => b.Text).Should().Equal("Log the rest", "Split the rest", "That's all", "Delete the photo");

        // The rest: the salad alone, as one meal.
        await handler.HandleAsync(_world.PressOnLast(_world.LastKeyboard()[0].Data), CancellationToken.None);
        var rest = _world.MealsPosted[1];
        rest.GetProperty("name").GetString().Should().Be("Salad");
        rest.GetProperty("calories").GetDecimal().Should().Be(60m);
        Absent(rest, "recipeId").Should().BeTrue();
        _world.LastKeyboard().Select(b => b.Text).Should().Equal("Delete the photo");

        // The photo: the API's delete, with the address the card carried.
        await handler.HandleAsync(_world.PressOnLast(PhotoCardDelete), CancellationToken.None);
        _world.PhotoDeletes.Should().Equal(Photo);
        _world.LastText.Should().Be("Photo deleted.");
        _world.LastKeyboard().Should().BeEmpty();
    }

    private const string PhotoCardDelete = "dp";

    private static bool Absent(System.Text.Json.JsonElement body, string name) =>
        !body.TryGetProperty(name, out var value) || value.ValueKind == System.Text.Json.JsonValueKind.Null;

    [Fact]
    public async Task APlateThatIsOnlyARecipe_OffersTheRecipeAndDiscard_AndNoLogItForTheWholeEstimate()
    {
        _world.AnalysisJson = Analysis(withSalad: false, imageUrl: null);
        var handler = _world.Handler();

        await handler.HandleAsync(TelegramFakeWorld.Photo(), CancellationToken.None);

        _world.LastKeyboard().Select(b => b.Text).Should().Equal("Log Doro wat, 1.5 servings", "Discard");
        _world.LastText.Should().NotContain("Your photo", "a photo that was not stored has nothing to delete");
    }

    [Fact]
    public async Task DeletingAPhotoFirst_LeavesTheEstimateToLog()
    {
        _world.AnalysisJson = Analysis(withSalad: true);
        var handler = _world.Handler();
        await handler.HandleAsync(TelegramFakeWorld.Photo(), CancellationToken.None);

        await handler.HandleAsync(_world.PressOnLast(PhotoCardDelete), CancellationToken.None);

        _world.LastText.Should().StartWith("Photo deleted.");
        _world.LastKeyboard().Select(b => b.Text).Should().Equal("Log Doro wat, 1.5 servings", "Log the rest", "Split the rest", "Discard");
        _world.MealsPosted.Should().BeEmpty();
    }

    [Fact]
    public async Task APhotoAlreadyGone_SaysSo()
    {
        _world.AnalysisJson = Analysis(withSalad: false);
        _world.PhotoDeleteStatus = HttpStatusCode.NotFound;
        var handler = _world.Handler();
        await handler.HandleAsync(TelegramFakeWorld.Photo(), CancellationToken.None);

        await handler.HandleAsync(_world.PressOnLast(PhotoCardDelete), CancellationToken.None);

        _world.LastText.Should().StartWith("That photo is already gone.");
    }

    [Fact]
    public async Task Fit_ShowsTheApisRanking_AndLogsOneServing_ThenRanksAgain()
    {
        _world.FitJson = $$"""
            {"date":"2026-10-22","caloriesLeft":640,"proteinLeft":40,"carbsLeft":null,"fatLeft":null,"note":null,
             "recipes":[
               {"rank":1,"recipeId":"{{Recipe}}","title":"Doro wat","score":0.8,"calories":360,"protein":28,"carbs":12,"fat":20,"caloriesLeftAfter":280,"proteinLeftAfter":12},
               {"rank":2,"recipeId":"99999999-9999-4999-8999-999999999999","title":"Pasta bake","score":0.2,"calories":800,"protein":30,"carbs":90,"fat":30,"caloriesLeftAfter":-160,"proteinLeftAfter":10}
             ]}
            """;
        var handler = _world.Handler();

        await handler.HandleAsync(Text("/fit"), CancellationToken.None);

        _world.LastText.Should().Contain("Left: 640 kcal, 40 g protein")
            .And.Contain("1. <b>Doro wat</b>: 360 kcal, 28 g protein a serving; leaves 280 kcal")
            .And.Contain("2. <b>Pasta bake</b>: 800 kcal, 30 g protein a serving; leaves 160 kcal over");
        _world.LastKeyboard().Select(b => b.Text).Should().Equal("Log Doro wat, 1 serving", "Log Pasta bake, 1 serving");

        await handler.HandleAsync(_world.PressOnLast(_world.LastKeyboard()[0].Data), CancellationToken.None);

        var meal = _world.MealsPosted.Should().ContainSingle().Subject;
        meal.GetProperty("recipeId").GetGuid().Should().Be(Recipe);
        meal.GetProperty("servings").GetDecimal().Should().Be(1m);
        _world.LastText.Should().StartWith("Logged Doro wat, 1 serving.").And.Contain("Recipes that fit");
    }

    [Fact]
    public async Task Fit_WithNothingToRank_SaysWhyInTheApisWords()
    {
        _world.FitJson = """{"date":"2026-10-22","caloriesLeft":null,"proteinLeft":null,"carbsLeft":null,"fatLeft":null,"note":"No daily target is set, so nothing can be ranked against what is left.","recipes":[]}""";
        var handler = _world.Handler();

        await handler.HandleAsync(Text("/fit"), CancellationToken.None);

        _world.LastText.Should().Contain("No daily target is set");
        _world.LastKeyboard().Should().BeEmpty();
    }
}
