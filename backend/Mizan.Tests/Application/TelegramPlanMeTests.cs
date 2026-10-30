extern alias telegram;
using System.Net;
using FluentAssertions;
using telegram::Mizan.Telegram.Bot;
using Xunit;
using static Mizan.Tests.Application.TelegramFakeWorld;

namespace Mizan.Tests.Application;

/// <summary>/planme at the bot's two wires: the words go to the generate route, the draft comes back as a card with Confirm and Discard.</summary>
public class TelegramPlanMeTests
{
    private const string PlanId = "55555555-5555-4555-8555-555555555555";

    private readonly TelegramFakeWorld _world = new()
    {
        GenerateJson = $$"""
            {"drafts":[{
              "plan":{"id":"{{PlanId}}","name":"High protein week","status":"Draft","startDate":"2026-10-23","endDate":"2026-10-29",
                      "durationDays":7,"entryCount":21,
                      "days":[{"dayIndex":0,"slots":[{"slot":"BREAKFAST","entries":[{"kind":"Food","foodName":"Eggs"}]},
                                                     {"slot":"LUNCH","entries":[{"kind":"Recipe","recipeTitle":"Shiro & injera"}]}]}]},
              "budgetCeiling":1500,
              "violations":[{"dayIndex":2,"slot":null,"rule":"protein_under","message":"95 g protein is under the 120 g target."}],
              "cost":{"regionCode":"ET-AA","currency":"ETB","total":1240,"weeklyTotal":1240,"weeklyBudget":1500,"pricedItems":9,"totalItems":11,
                      "coverage":0.82,"lowestConfidence":"Observed","items":[]},
              "unresolved":[{"dayIndex":4,"slot":"SNACK","text":"Kolo","reason":"Not found in the food catalogue."}],
              "note":"Cheap staples."}],
             "skipped":[]}
            """,
    };

    [Fact]
    public async Task PlanMe_SendsTheWordsAsThePreference_AndRepliesWithTheDraftAndTwoButtons()
    {
        await _world.Handler().HandleAsync(Text("/planme high protein, cheap"), CancellationToken.None);

        var posted = _world.PlansGenerated.Should().ContainSingle().Subject;
        posted.GetProperty("preference").GetString().Should().Be("high protein, cheap");
        posted.GetProperty("durationDays").GetInt32().Should().Be(PlanMeHandler.PlanDays);
        DateOnly.Parse(posted.GetProperty("startDate").GetString()!).Should().Be(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1));

        _world.LastText.Should().StartWith("<b>High protein week</b> (draft)")
            .And.Contain("About <b>ETB 1,240.00</b> a week (budget ETB 1,500.00), 9 of 11 items priced")
            .And.Contain("Day 1: Eggs, Shiro &amp; injera")
            .And.Contain("95 g protein is under the 120 g target.")
            .And.Contain("1 line(s) to review");
        _world.LastKeyboard().Should().Equal(("Confirm", $"pm:ok:{PlanId}"), ("Discard", $"pm:no:{PlanId}"));
        _world.LastKeyboard().Should().OnlyContain(b => System.Text.Encoding.UTF8.GetByteCount(b.Data) <= 64);
    }

    [Fact]
    public async Task Confirm_AcceptsTheDraft_AndDiscard_DeclinesIt()
    {
        await _world.Handler().HandleAsync(Press($"pm:ok:{PlanId}"), CancellationToken.None);
        _world.LastText.Should().Contain($"/meal-plan/{PlanId}");

        await _world.Handler().HandleAsync(Press($"pm:no:{PlanId}"), CancellationToken.None);
        _world.LastText.Should().StartWith("Discarded.");

        _world.PlanDecisions.Should().Equal($"{PlanId}/accept", $"{PlanId}/decline");
    }

    [Fact]
    public async Task PlanMe_OverTheAllowance_SaysSoInsteadOfACard()
    {
        _world.GenerateStatus = HttpStatusCode.TooManyRequests;

        await _world.Handler().HandleAsync(Text("/planme anything"), CancellationToken.None);

        _world.LastText.Should().Be("You have used your AI allowance for now.");
    }

    [Fact]
    public async Task PlanMe_WhenTheFeatureIsOffForTheHousehold_ShowsTheApisMessage()
    {
        _world.FeatureOff = true;

        await _world.Handler().HandleAsync(Text("/planme anything"), CancellationToken.None);

        _world.LastText.Should().Be("This feature is turned off for your household.");
        // The bot names itself on every call as the user, which is what lets the API hold it to telegram.bot.
        _world.SurfacesSent.Should().NotBeEmpty().And.OnlyContain(s => s == "telegram");
    }

    [Fact]
    public async Task PlanMe_WithNoWords_AsksForThem_AndCallsNothing()
    {
        await _world.Handler().HandleAsync(Text("/planme"), CancellationToken.None);

        _world.PlansGenerated.Should().BeEmpty();
        _world.LastText.Should().Contain("/planme high protein");
    }

    [Fact]
    public async Task A_draft_waits_for_the_persons_answer_in_the_assistant_conversation_and_Confirm_closes_the_flow()
    {
        await _world.Handler().HandleAsync(Text("/planme high protein, cheap"), CancellationToken.None);

        var asked = _world.ContextPosted.Should().ContainSingle().Subject;
        asked.GetProperty("holdMinutes").GetInt32().Should().Be(ChatContext.QuestionHoldMinutes);
        asked.GetProperty("note").GetString().Should()
            .Contain("drafted a meal plan for \"high protein, cheap\"")
            .And.Contain("High protein week (draft)")
            .And.Contain("Shiro & injera")
            .And.EndWith("with buttons: Confirm, Discard.");
        _world.PendingContext.Should().NotBeNull();

        await _world.Handler().HandleAsync(Press($"pm:ok:{PlanId}"), CancellationToken.None);

        var closed = _world.ContextPosted[^1];
        closed.GetProperty("holdMinutes").GetInt32().Should().Be(0);
        closed.GetProperty("note").GetString().Should().Be($"The person pressed Confirm on the /planme draft {PlanId}: it is now their active meal plan. That flow is finished.");
        _world.PendingContext.Should().BeNull();
    }
}
