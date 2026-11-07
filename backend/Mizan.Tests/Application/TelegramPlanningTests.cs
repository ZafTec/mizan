extern alias telegram;
using System.Net;
using System.Text;
using FluentAssertions;
using Xunit;
using static Mizan.Tests.Application.TelegramFakeWorld;

namespace Mizan.Tests.Application;

/// <summary>
/// The plan and the shopping list in a chat window. The bot renders what the API answers and relays one id per
/// button; the API decides what may be logged or ticked. Every button stays under Telegram's 64-byte limit.
/// </summary>
public class TelegramPlanningTests
{
    private readonly TelegramFakeWorld _world = new();

    // ---- /plan ----

    [Fact]
    public async Task Plan_RendersTheDay_WithSlotsLoggedMarksTotalsViolationsAndTheWindow()
    {
        await _world.Handler().HandleAsync(Text("/plan"), CancellationToken.None);

        _world.PlanDaysRequested.Should().ContainSingle().Which.Should().BeEmpty("without a date the API picks the user's today");
        var text = _world.LastText;
        text.Should().StartWith("<b>Cut week</b> · Thu 22 Oct · 1 of 4 logged\n");
        text.Should().Contain("<b>Breakfast</b>\nOats with milk, 80 g, 300 kcal ✓\n");
        text.Should().Contain("<b>Lunch</b>\nShiro &amp; injera, 1.5 servings, 650 kcal\n");
        text.Should().Contain("<b>Dinner</b>\nChicken breast, 200 g, 330 kcal\n");
        text.Should().Contain("<b>Snack</b>\nWhatever is in the fridge\n");
        text.Should().Contain("Calories: <b>1280</b> / 1800 kcal (71%)\nProtein: <b>110</b> / 140 g (79%)\nCarbs: <b>150</b> / 120 g (125%)\nFat: <b>40</b> g\n");
        text.Should().Contain("⚠ Day 1 has 150 g carbs, over the 120 g limit.");
        text.Should().EndWith("Eating window 08:00–20:00\n");
    }

    [Fact]
    public async Task Plan_OffersOneLogButtonPerUnloggedEntry_NoneForTextOrLoggedOnes_AndTomorrow()
    {
        await _world.Handler().HandleAsync(Text("/plan"), CancellationToken.None);

        var buttons = _world.LastKeyboard();
        buttons.Select(b => b.Text).Should().Equal("Log Shiro & injera", "Log Chicken breast", "Tomorrow");
        buttons[0].Data.Should().Be("pe:a2222222-2222-4222-8222-222222222222");
        buttons[1].Data.Should().Be("pe:a3333333-3333-4333-8333-333333333333");
        buttons[2].Data.Should().Be("pd:2026-10-23");
        buttons.Should().OnlyContain(b => Encoding.UTF8.GetByteCount(b.Data) <= 64);
        _world.PlanLogsPosted.Should().BeEmpty("a card is a proposal, not a write");
    }

    [Fact]
    public async Task PlanTomorrow_TakesTodayFromTheApi_AndAsksForTheNextDay()
    {
        await _world.Handler().HandleAsync(Text("/plan tomorrow"), CancellationToken.None);

        _world.PlanDaysRequested.Should().Equal(string.Empty, "?date=2026-10-23");
        _world.LastText.Should().StartWith("<b>Cut week</b> · Fri 23 Oct");
        _world.LastKeyboard().Last().Data.Should().Be("pd:2026-10-24");
    }

    [Fact]
    public async Task PlanWithADate_AsksForThatDay_AndAnythingElseGetsTheSyntax()
    {
        var handler = _world.Handler();

        await handler.HandleAsync(Text("/plan 2026-10-25"), CancellationToken.None);
        _world.PlanDaysRequested.Should().Equal("?date=2026-10-25");

        await handler.HandleAsync(Text("/plan next week"), CancellationToken.None);
        _world.LastText.Should().Contain("/plan tomorrow");
        _world.PlanDaysRequested.Should().HaveCount(1, "nonsense is not sent to the API");
    }

    [Fact]
    public async Task Plan_WithoutAnActivePlan_PointsAtTheWebsite()
    {
        _world.HasPlan = false;

        await _world.Handler().HandleAsync(Text("/plan"), CancellationToken.None);

        _world.LastText.Should().Be("No active plan covers today. Set one up at https://mizan.test/meal-plan.");
        _world.LastSend.TryGetProperty("reply_markup", out _).Should().BeFalse();
    }

    [Fact]
    public async Task TheTomorrowButton_RendersThatDay()
    {
        await _world.Handler().HandleAsync(Press("pd:2026-10-24"), CancellationToken.None);

        _world.PlanDaysRequested.Should().Equal("?date=2026-10-24");
        _world.LastText.Should().StartWith("<b>Cut week</b> · Sat 24 Oct");
    }

    // ---- Log from the plan ----

    [Fact]
    public async Task PressingLog_LogsThroughTheEntryRoute_AndRendersTheDayAgainWithTheTick()
    {
        var handler = _world.Handler();
        await handler.HandleAsync(Text("/plan"), CancellationToken.None);
        var log = _world.LastKeyboard().Single(b => b.Text == "Log Shiro & injera");

        await handler.HandleAsync(Press(log.Data), CancellationToken.None);

        var (url, body) = _world.PlanLogsPosted.Should().ContainSingle().Subject;
        url.Should().EndWith("api/MealPlans/55555555-5555-4555-8555-555555555555/entries/a2222222-2222-4222-8222-222222222222/log");
        body.EnumerateObject().Should().BeEmpty("the API dates and types the diary row from the entry");

        _world.LastText.Should().StartWith("Logged Shiro &amp; injera.\n\n<b>Cut week</b> · Thu 22 Oct · 2 of 4 logged\n");
        _world.LastText.Should().Contain("Shiro &amp; injera, 1.5 servings, 650 kcal ✓");
        _world.LastKeyboard().Select(b => b.Text).Should().Equal("Log Chicken breast", "Tomorrow");
    }

    [Fact]
    public async Task PressingLog_OnTomorrowsCard_FindsTheEntryOnTomorrow()
    {
        await _world.Handler().HandleAsync(Press("pe:a3333333-3333-4333-8333-333333333333"), CancellationToken.None);

        // Today's card has the entry too in this world, so only today is read before logging.
        _world.PlanDaysRequested.Take(1).Should().Equal(string.Empty);
        _world.PlanLogsPosted.Should().ContainSingle();
    }

    [Fact]
    public async Task AStaleLogButton_SaysTheCardIsTooOld_AndLogsNothing()
    {
        await _world.Handler().HandleAsync(Press("pe:deadbeef-dead-4ead-8ead-deadbeefdead"), CancellationToken.None);

        _world.PlanDaysRequested.Should().Equal(string.Empty, "?date=2026-10-23");
        _world.PlanLogsPosted.Should().BeEmpty();
        _world.LastText.Should().Contain("too old").And.Contain("/plan");
    }

    [Fact]
    public async Task WhenTheApiRefusesTheLog_ItsReasonIsRepeated_AndNothingIsClaimed()
    {
        _world.LogEntryStatus = HttpStatusCode.BadRequest;

        await _world.Handler().HandleAsync(Press("pe:a2222222-2222-4222-8222-222222222222"), CancellationToken.None);

        _world.LastText.Should().Be("A text entry has nothing to log.");
    }

    // ---- /shop ----

    [Fact]
    public async Task Shop_RendersTenUncheckedItems_GroupedBySession_WithAmountsAndTheFooter()
    {
        await _world.Handler().HandleAsync(Text("/shop"), CancellationToken.None);

        var text = _world.LastText;
        text.Should().StartWith("<b>Week 43</b>\n\n<b>Sunday batch</b>\n• 2 pcs Batch item 1\n• Batch item 2 (needs review)\n• 500 g Batch item 3\n• 2 pcs Batch item 4\n\n• Item 5\n");
        text.Should().Contain("• 500 g Item 9\n• 2 pcs Item 10\n").And.NotContain("Item 11");
        text.Should().EndWith("\n12 left of 12");
    }

    [Fact]
    public async Task Shop_OffersOneToggleButtonPerShownItem_AndMore()
    {
        await _world.Handler().HandleAsync(Text("/shop"), CancellationToken.None);

        var buttons = _world.LastKeyboard();
        buttons.Should().HaveCount(11);
        buttons.Take(10).Select(b => b.Text).Should().Equal(
            "☐ Batch item 1", "☐ Batch item 2", "☐ Batch item 3", "☐ Batch item 4",
            "☐ Item 5", "☐ Item 6", "☐ Item 7", "☐ Item 8", "☐ Item 9", "☐ Item 10");
        buttons[0].Data.Should().Be("sh:b0000001-0000-4000-8000-000000000000");
        buttons[10].Should().Be(("More", "sp:1"));
        buttons.Should().OnlyContain(b => Encoding.UTF8.GetByteCount(b.Data) <= 64);
    }

    [Fact]
    public async Task More_ShowsTheRestOfTheNewestList_WithBackAndNoMore()
    {
        await _world.Handler().HandleAsync(Press("sp:1"), CancellationToken.None);

        _world.LastText.Should().Be("<b>Week 43</b>\n\n• Item 11\n• 500 g Item 12\n\n12 left of 12");
        _world.LastKeyboard().Select(b => b.Text).Should().Equal("☐ Item 11", "☐ Item 12", "Back");
        _world.LastKeyboard().Last().Data.Should().Be("sp:0");
    }

    [Fact]
    public async Task Ticking_PatchesTheToggleRoute_AndRendersTheSamePageWithoutTheItem()
    {
        var handler = _world.Handler();
        await handler.HandleAsync(Text("/shop"), CancellationToken.None);

        await handler.HandleAsync(Press(_world.LastKeyboard()[2].Data), CancellationToken.None);

        var (url, body) = _world.TogglesPatched.Should().ContainSingle().Subject;
        url.Should().EndWith("api/ShoppingLists/items/b0000003-0000-4000-8000-000000000000/toggle");
        body.GetProperty("isChecked").GetBoolean().Should().BeTrue();

        _world.LastText.Should().NotContain("Batch item 3").And.Contain("• Item 11\n").And.EndWith("\n11 left of 12");
        _world.LastKeyboard().Select(b => b.Text).Should().Equal(
            "☐ Batch item 1", "☐ Batch item 2", "☐ Batch item 4",
            "☐ Item 5", "☐ Item 6", "☐ Item 7", "☐ Item 8", "☐ Item 9", "☐ Item 10", "☐ Item 11", "More");
    }

    [Fact]
    public async Task TickingTheLastItemOfTheLastPage_LandsOnThePageBefore()
    {
        foreach (var n in Enumerable.Range(0, 10)) _world.ShopItems[n] = _world.ShopItems[n] with { Checked = true };

        await _world.Handler().HandleAsync(Press("sh:b0000011-0000-4000-8000-000000000000"), CancellationToken.None);

        _world.LastText.Should().Be("<b>Week 43</b>\n\n• 500 g Item 12\n\n1 left of 12");
        _world.LastKeyboard().Select(b => b.Text).Should().Equal("☐ Item 12");
    }

    [Fact]
    public async Task AStaleToggleButton_SaysTheCardIsTooOld_AndTicksNothing()
    {
        await _world.Handler().HandleAsync(Press("sh:deadbeef-dead-4ead-8ead-deadbeefdead"), CancellationToken.None);

        _world.TogglesPatched.Should().BeEmpty();
        _world.LastText.Should().Contain("too old").And.Contain("/shop");
    }

    [Fact]
    public async Task Shop_WithoutAList_PointsAtTheWebsite()
    {
        _world.HasShoppingList = false;

        await _world.Handler().HandleAsync(Text("/shop"), CancellationToken.None);

        _world.LastText.Should().Be("You have no shopping list yet. Make one from your plan at https://mizan.test/meal-plan.");
    }

    // ---- Gating and help ----

    [Fact]
    public async Task ALapsedAccount_GetsTheUpgradePath_NotThePlan()
    {
        _world.IsPro = false;
        var handler = _world.Handler();

        await handler.HandleAsync(Text("/plan"), CancellationToken.None);
        await handler.HandleAsync(Press("pe:a2222222-2222-4222-8222-222222222222"), CancellationToken.None);

        _world.PlanDaysRequested.Should().BeEmpty();
        _world.PlanLogsPosted.Should().BeEmpty();
        _world.LastText.Should().Contain("Mizan Pro");
    }

    [Fact]
    public async Task Help_ListsPlanAndShop()
    {
        await _world.Handler().HandleAsync(Text("/help"), CancellationToken.None);

        _world.LastText.Should().Contain("<b>/plan</b>").And.Contain("<b>/shop</b>");
    }
}
