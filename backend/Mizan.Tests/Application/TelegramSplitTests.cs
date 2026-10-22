extern alias telegram;
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using telegram::Mizan.Telegram.Bot;
using Xunit;
using static Mizan.Tests.Application.TelegramFakeWorld;

namespace Mizan.Tests.Application;

/// <summary>
/// A meal card offers three things: log it, split it with the people who ate it, or discard it. The bot holds
/// no state, so each button carries what it needs, and none may go over Telegram's 64-byte limit. The bot never
/// divides anything: a split sends the whole meal plus the head count, and the API logs the share.
/// </summary>
public class TelegramSplitTests
{
    private readonly TelegramFakeWorld _world = new();

    [Fact]
    public async Task APhoto_GetsLogSplitAndDiscard()
    {
        var handler = _world.Handler();

        await handler.HandleAsync(Photo(), CancellationToken.None);

        var buttons = _world.LastKeyboard();
        buttons.Select(b => b.Text).Should().Equal("Log it", "Split", "Discard");
        buttons[0].Data.Should().StartWith("log:");
        buttons[1].Data.Should().StartWith("split:");
        buttons[2].Data.Should().Be("discard");
        _world.MealsPosted.Should().BeEmpty("a proposal is never a silent write");
    }

    [Fact]
    public async Task EveryButton_StaysUnderTelegramsByteLimit_EvenForALongNameInAnotherScript()
    {
        _world.FoodName = string.Concat(Enumerable.Repeat("የኢትዮጵያ ምግብ ", 12));
        var handler = _world.Handler();

        await handler.HandleAsync(Photo(), CancellationToken.None);

        var card = _world.LastKeyboard();
        card.Should().OnlyContain(b => Encoding.UTF8.GetByteCount(b.Data) <= 64);

        await handler.HandleAsync(Press(card[1].Data), CancellationToken.None);
        var picker = _world.LastKeyboard();
        picker.Should().OnlyContain(b => Encoding.UTF8.GetByteCount(b.Data) <= 64);
        picker.Should().Contain(b => b.Data.StartsWith("share:5|"));
    }

    [Fact]
    public async Task Split_AsksForThePeople_AndLogsNothingYet()
    {
        var handler = _world.Handler();
        await handler.HandleAsync(Photo(), CancellationToken.None);

        await handler.HandleAsync(Press(_world.LastKeyboard()[1].Data), CancellationToken.None);

        _world.LastKeyboard().Select(b => b.Text).Should().Equal("2", "3", "4", "5", "Log it all", "Discard");
        _world.LastText.Should().Contain("How many people");
        _world.MealsPosted.Should().BeEmpty();
    }

    [Fact]
    public async Task ChoosingThree_SendsTheWholeMealWithTheHeadCount_AndDividesNothingItself()
    {
        var handler = _world.Handler();
        await handler.HandleAsync(Photo(), CancellationToken.None);
        await handler.HandleAsync(Press(_world.LastKeyboard()[1].Data), CancellationToken.None);

        await handler.HandleAsync(Press(_world.LastKeyboard().Single(b => b.Text == "3").Data), CancellationToken.None);

        var meal = _world.MealsPosted.Should().ContainSingle().Subject;
        meal.GetProperty("calories").GetDecimal().Should().Be(600m, "the API owns the division");
        meal.GetProperty("proteinGrams").GetDecimal().Should().Be(30m);
        meal.GetProperty("carbsGrams").GetDecimal().Should().Be(60m);
        meal.GetProperty("fatGrams").GetDecimal().Should().Be(20m);
        meal.GetProperty("sharedBetween").GetInt32().Should().Be(3);
        meal.GetProperty("name").GetString().Should().Be("Pasta", "the API labels the share");
        meal.GetProperty("mealType").GetString().Should().Be("SNACK");
        _world.LastText.Should().Contain("1/3 share");
    }

    [Fact]
    public async Task LogItAll_FromThePicker_LogsTheWholeMeal()
    {
        var handler = _world.Handler();
        await handler.HandleAsync(Photo(), CancellationToken.None);
        await handler.HandleAsync(Press(_world.LastKeyboard()[1].Data), CancellationToken.None);

        await handler.HandleAsync(Press(_world.LastKeyboard().Single(b => b.Text == "Log it all").Data), CancellationToken.None);

        var meal = _world.MealsPosted.Should().ContainSingle().Subject;
        meal.GetProperty("calories").GetDecimal().Should().Be(600m);
        meal.GetProperty("sharedBetween").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task LogIt_StillLogsTheWholeMeal_AsBefore()
    {
        var handler = _world.Handler();
        await handler.HandleAsync(Photo(), CancellationToken.None);

        await handler.HandleAsync(Press(_world.LastKeyboard()[0].Data), CancellationToken.None);

        var meal = _world.MealsPosted.Should().ContainSingle().Subject;
        meal.GetProperty("calories").GetDecimal().Should().Be(600m);
        meal.GetProperty("name").GetString().Should().NotContain("share");
        meal.GetProperty("sharedBetween").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Discard_LogsNothing()
    {
        var handler = _world.Handler();
        await handler.HandleAsync(Photo(), CancellationToken.None);

        await handler.HandleAsync(Press("discard"), CancellationToken.None);

        _world.MealsPosted.Should().BeEmpty();
        _world.LastText.Should().Contain("Discarded");
    }

    [Theory]
    [InlineData("share:1|600|30|60|20|Pasta")]
    [InlineData("share:9|600|30|60|20|Pasta")]
    [InlineData("share:0|600|30|60|20|Pasta")]
    [InlineData("share:x|600|30|60|20|Pasta")]
    [InlineData("share:3|nonsense")]
    [InlineData("split:nonsense")]
    public async Task AButtonThatMakesNoSense_LogsNothing(string data)
    {
        var handler = _world.Handler();

        await handler.HandleAsync(Press(data), CancellationToken.None);

        _world.MealsPosted.Should().BeEmpty();
        _world.LastText.Should().Contain("too old");
    }

    [Fact]
    public async Task ALapsedAccount_CannotSplit()
    {
        _world.IsPro = false;
        var handler = _world.Handler();

        await handler.HandleAsync(Press("share:3|600|30|60|20|Pasta"), CancellationToken.None);

        _world.MealsPosted.Should().BeEmpty();
        _world.LastText.Should().Contain("Mizan Pro");
    }

    [Fact]
    public async Task WhenTheApiRefusesTheShare_TheUserIsToldAndNothingIsClaimed()
    {
        _world.MealsStatus = HttpStatusCode.BadRequest;
        var handler = _world.Handler();

        await handler.HandleAsync(Press("share:2|600|30|60|20|Pasta"), CancellationToken.None);

        _world.LastText.Should().NotContain("Logged your");
    }

    [Theory]
    [InlineData("Pasta", 100, "Pasta")]
    [InlineData("Pasta", 3, "Pas")]
    [InlineData("ምግብ", 6, "ምግ")]
    [InlineData("ምግብ", 5, "ም")]
    [InlineData("anything", 0, "")]
    public void TruncatingByBytes_NeverCutsACharacterInHalf(string value, int bytes, string expected)
    {
        UpdateHandler.TruncateUtf8(value, bytes).Should().Be(expected);
    }
}
