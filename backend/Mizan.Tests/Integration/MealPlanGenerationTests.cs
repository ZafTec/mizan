using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Ai;
using Mizan.Application.Interfaces;
using Mizan.Application.Planning;
using Mizan.Contracts.Ai;
using Mizan.Contracts.Planning;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;
using Mizan.Domain.Pricing;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The assistant drafts a plan, the person reviews it and confirms it. docs/PLANNING.md step 4.
/// The provider is the fixture's scripted fake: each story decides what the model answered and
/// checks what the API made of it.
/// </summary>
[Collection("ApiIntegration")]
public class MealPlanGenerationTests
{
    private static readonly DateOnly Monday = new(2026, 11, 2);

    private readonly ApiTestFixture _fixture;
    private readonly PlanningWorld _world;

    public MealPlanGenerationTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _world = new PlanningWorld(fixture);
    }

    [Fact]
    public async Task APersonGeneratesAPlanWithinBudget_AndGetsADraftWithViolationsAndCost()
    {
        var (me, client) = await StartAsync(pro: true);
        var eggs = await _world.FoodAsync("Eggs", 143, 12.6m, 0.7m, 9.5m);
        var lentils = await _world.FoodAsync("Lentils", 116, 9, 20, 0.4m);
        await PriceAsync(eggs, 200);
        await PriceAsync(lentils, 100);
        // The same answer for both budgets: one draft each, priced the same, judged against its own ceiling.
        // Each draft breaks a rule, so each gets one repair call, and the model sends the same plan back.
        for (var i = 0; i < 4; i++) _fixture.Ai.Reply(TwoDays("Eggs", "Lentils"));

        var response = await client.PostAsJsonAsync("/api/MealPlans/generate", new GenerateMealPlanRequest
        {
            StartDate = Monday,
            DurationDays = 2,
            Preference = "cheap eggs and lentils",
            Rules = new PlanRulesDto { MacroMode = "HighProtein", TargetProtein = 100 },
            BudgetCeilings = [1000, 300],
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var result = (await response.Content.ReadFromJsonAsync<GenerateMealPlanResult>())!;
        result.Drafts.Should().HaveCount(2);
        result.Skipped.Should().BeEmpty();

        var withinBudget = result.Drafts[0];
        withinBudget.BudgetCeiling.Should().Be(1000);
        withinBudget.Plan.Status.Should().Be("Draft");
        withinBudget.Plan.GeneratedFrom.Should().Match<PlanGenerationDto>(g =>
            g.PromptKey == AiPromptKeys.MealPlan && g.Model == "test-model" && g.BudgetCeiling == 1000 && g.Currency == "ETB");
        withinBudget.Plan.Rules.WeeklyBudget.Should().Be(1000);
        // The model named foods and grams only; code computed the day: 150 g eggs and 200 g lentils.
        withinBudget.Plan.Days[0].Totals.Calories.Should().Be(214.5m + 232m);
        withinBudget.Plan.Days[0].Totals.ProteinGrams.Should().Be(18.9m + 18m);
        // 250 g eggs at 200 a kilo and 500 g lentils at 100 a kilo, over two days, scaled to a week.
        withinBudget.Cost.Should().Match<PlanCostDto>(c =>
            c.Currency == "ETB" && c.Total == 100m && c.WeeklyTotal == 350m && c.PricedItems == 2 && c.TotalItems == 2);
        withinBudget.Plan.WeeklyCost.Should().Be(350m);
        // A plain read of the saved draft costs it the same way, day by day.
        var read = await PlanningWorld.PlanAsync(client, withinBudget.Plan.Id);
        read.Should().Match<MealPlanDetailDto>(p =>
            p.Currency == "ETB" && p.TotalCost == 100m && p.WeeklyCost == 350m && p.OverBudget == false && p.CostCoverage == 1m);
        read.Days.Select(d => d.Cost).Should().Equal(withinBudget.Plan.Days.Select(d => d.Cost)).And.NotContainNulls();
        withinBudget.Violations.Select(v => v.Rule).Should().Equal(PlanRuleNames.ProteinUnder, PlanRuleNames.ProteinUnder);
        withinBudget.Unresolved.Should().BeEmpty();

        var tooTight = result.Drafts[1];
        tooTight.BudgetCeiling.Should().Be(300);
        tooTight.Plan.Name.Should().Contain("ETB 300.00/week");
        tooTight.Violations.Select(v => v.Rule).Should().Contain(PlanRuleNames.BudgetOver);

        // Each ceiling was its own structured call on the planning line, and the model was shown prices, not asked for them.
        _fixture.Ai.Calls.Should().HaveCount(4);
        _fixture.Ai.Calls.Should().OnlyContain(c => c.ResponseSchema!.Name == "meal_plan_v1");
        _fixture.Ai.Calls[0].Messages.Should().Contain(m => m.Content.Contains("Eggs | 143 kcal") && m.Content.Contains("| 200"));
        // The repair repeats the conversation, then the draft, then the checks' own findings.
        _fixture.Ai.Calls[1].Messages[^2].Role.Should().Be(AiRole.Assistant);
        _fixture.Ai.Calls[1].Messages[^1].Content.Should().StartWith("The plan was checked").And.Contain("Day 0").And.Contain("protein");
        var ledger = await LedgerAsync();
        ledger.Should().HaveCount(4).And.OnlyContain(l => l.Feature == AiFeatures.Planning && l.Outcome == AiCallOutcome.Succeeded);

        // Both drafts are saved and listed as drafts of their owner.
        var drafts = await client.GetFromJsonAsync<Mizan.Application.Common.PagedResult<MealPlanSummaryDto>>("/api/MealPlans?status=Draft");
        drafts!.Items.Select(p => p.Id).Should().BeEquivalentTo(result.Drafts.Select(d => d.Plan.Id));
        drafts.Items.Should().OnlyContain(p => p.UserId == me && p.GeneratedFrom != null);
    }

    [Fact]
    public async Task ADailyBudgetIsToldToTheModel_AndTheDayOverItIsMarked()
    {
        var (_, client) = await StartAsync();
        var eggs = await _world.FoodAsync("Eggs", 143, 12.6m, 0.7m, 9.5m);
        var lentils = await _world.FoodAsync("Lentils", 116, 9, 20, 0.4m);
        await PriceAsync(eggs, 200);
        await PriceAsync(lentils, 100);
        _fixture.Ai.Reply("""
            {"name":"Two days","note":"Simple.","prepSessions":[],"days":[
              {"dayIndex":0,"entries":[{"slot":"LUNCH","kind":"food","foodName":"Eggs","recipeId":null,"grams":300,"servings":null,"note":null,"prepSessionIndex":null}]},
              {"dayIndex":1,"entries":[{"slot":"LUNCH","kind":"food","foodName":"Lentils","recipeId":null,"grams":200,"servings":null,"note":null,"prepSessionIndex":null}]}
            ]}
            """);

        var response = await client.PostAsJsonAsync("/api/MealPlans/generate", Request() with { DailyBudget = 40 });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var draft = (await response.Content.ReadFromJsonAsync<GenerateMealPlanResult>())!.Drafts.Single();
        _fixture.Ai.Calls.Single().Messages.Should().Contain(m => m.Content.Contains("Daily budget: ETB 40.00"));
        draft.Plan.Rules.Should().Match<PlanRulesDto>(r => r.DailyBudget == 40 && r.Currency == "ETB");
        // 300 g eggs at 200 a kilo is 60; 200 g lentils at 100 a kilo is 20.
        draft.Plan.Days.Select(d => (d.Cost, d.OverBudget)).Should().Equal((60m, true), (20m, false));
        draft.Violations.Where(v => v.Rule == PlanRuleNames.DailyBudgetOver).Select(v => v.DayIndex).Should().Equal(0);

        // The plan page reads the same mark.
        var read = await PlanningWorld.PlanAsync(client, draft.Plan.Id);
        read.Days[0].OverBudget.Should().BeTrue();
        read.Days[0].Violations.Should().Contain(v => v.Rule == PlanRuleNames.DailyBudgetOver && v.Message.Contains("over the ETB 40.00 daily budget"));
    }

    [Fact]
    public async Task AFoodTheCatalogueDoesNotHave_BecomesATextEntryForReview()
    {
        var (_, client) = await StartAsync();
        var eggs = await _world.FoodAsync("Eggs", 143, 12.6m, 0.7m, 9.5m);
        var lentils = await _world.FoodAsync("Lentils", 116, 9, 20, 0.4m);
        _fixture.Ai.Reply("""
            {"name":"Week","note":"Simple.","prepSessions":[],"days":[{"dayIndex":0,"entries":[
              {"slot":"BREAKFAST","kind":"food","foodName":"eggs","recipeId":null,"grams":120,"servings":null,"note":null,"prepSessionIndex":null},
              {"slot":"LUNCH","kind":"food","foodName":"Grilled lentils","recipeId":null,"grams":250,"servings":null,"note":null,"prepSessionIndex":null},
              {"slot":"DINNER","kind":"food","foodName":"Dragonfruit salad","recipeId":null,"grams":200,"servings":null,"note":"Fresh","prepSessionIndex":null}
            ]}]}
            """);

        var draft = await GenerateOneAsync(client, durationDays: 1);

        var entries = PlanningWorld.Entries(draft.Plan.Days[0]).ToList();
        // An exact name, ignoring case, and a name found by search both became catalogue foods.
        entries.Single(e => e.Slot == "BREAKFAST").Should().Match<PlanEntryDto>(e => e.Kind == "Food" && e.FoodId == eggs.Id && e.Grams == 120);
        entries.Single(e => e.Slot == "LUNCH").Should().Match<PlanEntryDto>(e => e.Kind == "Food" && e.FoodId == lentils.Id);
        // The one nothing matched is kept as text, with a note saying what to check, and named in the result.
        var dinner = entries.Single(e => e.Slot == "DINNER");
        dinner.Kind.Should().Be("Text");
        dinner.Text.Should().Be("Dragonfruit salad");
        dinner.Nutrition.Should().BeNull();
        dinner.Notes.Should().Be($"Fresh Review: {MealPlanDraftReader.NotInCatalogue}");
        draft.Unresolved.Should().ContainSingle().Which.Should().Be(
            new UnresolvedPlanLineDto(0, "DINNER", "Dragonfruit salad", MealPlanDraftReader.NotInCatalogue));
        draft.Violations.Select(v => v.Rule).Should().Contain(PlanRuleNames.Unresolved);
        draft.Cost.Items.Should().Contain(i => i.Name == "Dragonfruit salad" && i.Cost == null);
    }

    [Fact]
    public async Task TheFreeAllowanceRefusesASecondPlanOnTheSameDay_AndLeavesChatAlone()
    {
        var (_, client) = await StartAsync();
        await _world.FoodAsync("Eggs", 143, 12.6m, 0.7m, 9.5m);
        _fixture.Ai.Reply(TwoDays("Eggs", "Eggs"));
        await GenerateOneAsync(client);

        var second = await client.PostAsJsonAsync("/api/MealPlans/generate", Request());

        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        second.Headers.RetryAfter.Should().NotBeNull();
        var problem = await second.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errorCode").GetString().Should().Be("ai_quota_exceeded");
        problem.GetProperty("scope").GetString().Should().Be("user");
        _fixture.Ai.Calls.Should().HaveCount(1, "the refusal comes before the provider is called");

        // Planning has its own line: the day's chat allowance is untouched.
        var chat = await client.PostAsJsonAsync("/api/Ai/chat", new { message = "Hello?" });
        chat.StatusCode.Should().Be(HttpStatusCode.OK);
        var usage = await client.GetFromJsonAsync<JsonElement>("/api/Ai/usage");
        usage.GetProperty("planning").GetProperty("requestsUsed").GetInt32().Should().Be(1);
        usage.GetProperty("planning").GetProperty("requestLimit").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task AFailedProviderCallStillSettlesItsReservation_AndSavesNoDraft()
    {
        var (me, client) = await StartAsync();
        await _world.FoodAsync("Eggs", 143, 12.6m, 0.7m, 9.5m);
        _fixture.Ai.Fail("The provider is down.");

        var response = await client.PostAsJsonAsync("/api/MealPlans/generate", Request());

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().Should().Be("ai_unavailable");
        var ledger = await LedgerAsync();
        ledger.Should().ContainSingle().Which.Should().Match<AiUsageLog>(l =>
            l.Feature == AiFeatures.Planning && l.Outcome == AiCallOutcome.ProviderError && l.UserId == me);
        (await _world.DbAsync(db => db.MealPlans.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task AnAnswerThatBreaksTheSchemaIsAFailedCall_NotAScrapedPlan()
    {
        var (_, client) = await StartAsync();
        await _world.FoodAsync("Eggs", 143, 12.6m, 0.7m, 9.5m);
        _fixture.Ai.Reply("Here is a lovely plan: eggs on Monday, lentils on Tuesday.");

        var response = await client.PostAsJsonAsync("/api/MealPlans/generate", Request());

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await _world.DbAsync(db => db.MealPlans.CountAsync())).Should().Be(0);
        (await LedgerAsync()).Should().ContainSingle().Which.Outcome.Should().NotBe(AiCallOutcome.Pending);
    }

    [Fact]
    public async Task ConfirmingAGeneratedDraftMakesItTheActivePlan()
    {
        var (_, client) = await StartAsync();
        await _world.FoodAsync("Eggs", 143, 12.6m, 0.7m, 9.5m);
        var current = await PlanningWorld.CreatePlanAsync(client, new CreateMealPlanRequest { Name = "Old", StartDate = Monday, DurationDays = 7 });
        _fixture.Ai.Reply(TwoDays("Eggs", "Eggs"));
        var draft = await GenerateOneAsync(client);

        // A draft is not followed: today's plan is still the old one.
        (await client.GetFromJsonAsync<PlanDayViewDto>($"/api/MealPlans/day?date={Monday:yyyy-MM-dd}"))!.PlanId.Should().Be(current);
        // It can be read and edited before it is confirmed.
        var day = await client.GetFromJsonAsync<PlanDayViewDto>($"/api/MealPlans/day?planId={draft.Plan.Id}");
        day!.Day.EntryCount.Should().Be(2, "the first day has breakfast and lunch");
        (await PlanningWorld.PlanAsync(client, draft.Plan.Id)).CanEdit.Should().BeTrue();

        var accept = await client.PostAsync($"/api/MealPlans/{draft.Plan.Id}/accept", null);

        accept.StatusCode.Should().Be(HttpStatusCode.NoContent, await accept.Content.ReadAsStringAsync());
        (await PlanningWorld.PlanAsync(client, draft.Plan.Id)).Status.Should().Be("Active");
        (await PlanningWorld.PlanAsync(client, current)).Status.Should().Be("Archived");
        (await client.GetFromJsonAsync<PlanDayViewDto>($"/api/MealPlans/day?date={Monday:yyyy-MM-dd}"))!.PlanId.Should().Be(draft.Plan.Id);

        // Once confirmed it is not a draft any more, so there is nothing left to accept.
        (await client.PostAsync($"/api/MealPlans/{draft.Plan.Id}/accept", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TheAssistantDraftsAPlanInChat_AndTheTurnCarriesTypedCards()
    {
        var (me, client) = await StartAsync();
        await _fixture.GrantAiConsentAsync(me);
        await _world.FoodAsync("Eggs", 143, 12.6m, 0.7m, 9.5m);
        _fixture.Ai.CallTools(("generate_meal_plan", $$"""{"startDate":"{{Monday:yyyy-MM-dd}}","durationDays":2,"preference":"eggs"}"""));
        _fixture.Ai.Reply(TwoDays("Eggs", "Eggs"));
        _fixture.Ai.Reply("Here is a draft for you to look at.");

        var response = await client.PostAsJsonAsync("/api/Ai/chat", new { message = "Plan two days of eggs." });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var turn = (await response.Content.ReadFromJsonAsync<AiChatTurnDto>())!;
        turn.Performed.Should().ContainSingle().Which.Succeeded.Should().BeTrue();
        turn.Cards.Select(c => c.Type).Should().Equal(AiCardTypes.PlanPreview, AiCardTypes.CostTable);
        var preview = turn.Cards[0].Data.Deserialize<PlanPreviewCardData>(JsonSerializerOptions.Web)!;
        preview.Status.Should().Be("Draft");
        preview.EntryCount.Should().Be(4);
        (await PlanningWorld.PlanAsync(client, preview.PlanId)).GeneratedFrom.Should().NotBeNull();
        // The model read back the draft's ids so it can edit it, and the chat and the plan were billed on their own lines.
        _fixture.Ai.LastCall.Messages.Should().Contain(m => m.Role == Mizan.Application.Interfaces.AiRole.Tool && m.Content.Contains(preview.PlanId.ToString()));
        (await LedgerAsync()).Select(l => l.Feature).Should().BeEquivalentTo([AiFeatures.Chat, AiFeatures.Planning, AiFeatures.Chat]);
    }

    [Fact]
    public async Task TheAssistantChangesOnlyADraft_NotAPlanThePersonFollows()
    {
        var (me, client) = await StartAsync();
        await _fixture.GrantAiConsentAsync(me);
        var eggs = await _world.FoodAsync("Eggs", 143, 12.6m, 0.7m, 9.5m);
        var active = await PlanningWorld.CreatePlanAsync(client, new CreateMealPlanRequest
        {
            StartDate = Monday,
            DurationDays = 1,
            Entries = [PlanningWorld.FoodEntry(eggs.Id, 100, day: 0)],
        });
        var entryId = (await PlanningWorld.PlanAsync(client, active)).Days[0].Slots[0].Entries[0].Id;
        _fixture.Ai.CallTools(("remove_plan_entry", $$"""{"planId":"{{active}}","entryId":"{{entryId}}"}"""));
        _fixture.Ai.Reply("I could not change that plan.");

        var turn = (await (await client.PostAsJsonAsync("/api/Ai/chat", new { message = "Drop the eggs." }))
            .Content.ReadFromJsonAsync<AiChatTurnDto>())!;

        turn.Performed.Should().ContainSingle().Which.Succeeded.Should().BeFalse();
        (await PlanningWorld.PlanAsync(client, active)).EntryCount.Should().Be(1);
    }

    [Fact]
    public async Task AnEmptyCatalogueRefusesThePlanWithPlanNeedsFoods_BeforeTheAssistantIsAsked()
    {
        var (_, client) = await StartAsync();

        var response = await client.PostAsJsonAsync("/api/MealPlans/generate", Request());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = (await response.Content.ReadFromJsonAsync<PlanNeedsFoodsError>())!;
        problem.ErrorCode.Should().Be("plan_needs_foods");
        problem.EmptyDays.Should().Equal(0, 1);
        problem.Error.Should().Contain("import the starter foods");
        _fixture.Ai.Calls.Should().BeEmpty("nothing is spent on a plan that cannot have foods");
        (await LedgerAsync()).Should().BeEmpty();
        (await _world.DbAsync(db => db.MealPlans.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task ADraftWithADayOfFoodsTheCatalogueLacks_IsRefused_NamingTheFoodsAndTheDay()
    {
        var (_, client) = await StartAsync();
        await _world.FoodAsync("Eggs", 143, 12.6m, 0.7m, 9.5m);
        _fixture.Ai.Reply(EggsThen("Dragonfruit salad"));

        var response = await client.PostAsJsonAsync("/api/MealPlans/generate", Request());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await response.Content.ReadAsStringAsync());
        var problem = (await response.Content.ReadFromJsonAsync<PlanNeedsFoodsError>())!;
        problem.ErrorCode.Should().Be("plan_needs_foods");
        problem.EmptyDays.Should().Equal(1);
        problem.MissingFoods.Should().Equal("Dragonfruit salad");
        problem.Error.Should().StartWith("No draft was saved: the catalogue has no foods for day 2 of the plan. Not in the catalogue: Dragonfruit salad.");
        (await _world.DbAsync(db => db.MealPlans.CountAsync())).Should().Be(0);
        // Free has one planning request a day, so the repair is refused by the allowance and settled as refused.
        (await LedgerAsync()).Select(l => l.Outcome).Should().BeEquivalentTo([AiCallOutcome.Succeeded, AiCallOutcome.Refused]);
        _fixture.Ai.Calls.Should().ContainSingle();
    }

    [Fact]
    public async Task ADraftWithADayTheCatalogueLacks_IsRepairedOnce_AndTheRepairedDraftIsSaved()
    {
        var (_, client) = await StartAsync(pro: true);
        await _world.FoodAsync("Eggs", 143, 12.6m, 0.7m, 9.5m);
        _fixture.Ai.Reply(EggsThen("Dragonfruit salad"));
        _fixture.Ai.Reply(EggsThen("Eggs"));

        var response = await client.PostAsJsonAsync("/api/MealPlans/generate", Request());

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var draft = (await response.Content.ReadFromJsonAsync<GenerateMealPlanResult>())!.Drafts.Single();
        draft.Plan.Days.Should().OnlyContain(d => PlanningWorld.Entries(d).Any(e => e.Kind == "Food"));
        draft.Unresolved.Should().BeEmpty();
        _fixture.Ai.Calls.Should().HaveCount(2);
        _fixture.Ai.Calls[1].Messages[^1].Content.Should().Contain("day 2").And.Contain("Dragonfruit salad");
        (await LedgerAsync()).Should().HaveCount(2).And.OnlyContain(l => l.Outcome == AiCallOutcome.Succeeded);
        (await _world.DbAsync(db => db.MealPlans.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task WhenTheRepairCallFails_TheFirstDraftStands_AndBothCallsAreSettled()
    {
        var (_, client) = await StartAsync(pro: true);
        await _world.FoodAsync("Eggs", 143, 12.6m, 0.7m, 9.5m);
        _fixture.Ai.Reply(TwoDays("Eggs", "Eggs"));
        _fixture.Ai.Fail("The provider timed out.");

        var response = await client.PostAsJsonAsync("/api/MealPlans/generate", Request() with
        {
            Rules = new PlanRulesDto { MacroMode = "HighProtein", TargetProtein = 300 },
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var draft = (await response.Content.ReadFromJsonAsync<GenerateMealPlanResult>())!.Drafts.Single();
        draft.Violations.Select(v => v.Rule).Should().Contain(PlanRuleNames.ProteinUnder);
        var ledger = await LedgerAsync();
        ledger.Should().HaveCount(2).And.OnlyContain(l => l.Outcome != AiCallOutcome.Pending);
        ledger.Count(l => l.Outcome == AiCallOutcome.Succeeded).Should().Be(1);
    }

    private static string EggsThen(string secondDay) => $$"""
        {"name":"Week","note":null,"prepSessions":[],"days":[
          {"dayIndex":0,"entries":[{"slot":"BREAKFAST","kind":"food","foodName":"Eggs","recipeId":null,"grams":120,"servings":null,"note":null,"prepSessionIndex":null}]},
          {"dayIndex":1,"entries":[{"slot":"LUNCH","kind":"food","foodName":"{{secondDay}}","recipeId":null,"grams":200,"servings":null,"note":null,"prepSessionIndex":null}]}
        ]}
        """;

    [Fact]
    public async Task AfterTheStarterFoodsAndStaplePrices_AKetoWeekHasFoodsEveryDay_StaysUnderItsCarbs_AndIsPriced()
    {
        var (_, client) = await StartAsync();
        var (_, admin) = await _world.PersonAsync("admin", role: "admin");
        (await admin.PostAsync("/api/admin/prices/import-starter-foods", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsync("/api/admin/prices/seed-staples", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var days = string.Join(",", Enumerable.Range(0, 7).Select(d => $$"""
            {"dayIndex":{{d}},"entries":[
              {"slot":"BREAKFAST","kind":"food","foodName":"Eggs","recipeId":null,"grams":150,"servings":null,"note":null,"prepSessionIndex":null},
              {"slot":"LUNCH","kind":"food","foodName":"Chicken breast","recipeId":null,"grams":250,"servings":null,"note":null,"prepSessionIndex":null},
              {"slot":"DINNER","kind":"food","foodName":"Kale","recipeId":null,"grams":100,"servings":null,"note":null,"prepSessionIndex":null},
              {"slot":"DINNER","kind":"food","foodName":"Niter kibbeh","recipeId":null,"grams":20,"servings":null,"note":null,"prepSessionIndex":null},
              {"slot":"SNACK","kind":"food","foodName":"Avocado","recipeId":null,"grams":100,"servings":null,"note":null,"prepSessionIndex":null}]}
            """));
        // A fenced reply with a sentence before it is still read: the JSON object inside is the answer.
        _fixture.Ai.Reply($"Here is your keto week.\n```json\n{{\"name\":\"Keto week\",\"note\":null,\"prepSessions\":[],\"days\":[{days}]}}\n```");

        var response = await client.PostAsJsonAsync("/api/MealPlans/generate", new GenerateMealPlanRequest
        {
            StartDate = Monday,
            DurationDays = 7,
            Rules = new PlanRulesDto { MacroMode = "Keto" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var draft = (await response.Content.ReadFromJsonAsync<GenerateMealPlanResult>())!.Drafts.Single();
        draft.Plan.Days.Should().HaveCount(7).And.OnlyContain(d => PlanningWorld.Entries(d).Any(e => e.Kind == "Food"));
        draft.Unresolved.Should().BeEmpty();
        draft.Plan.EffectiveTargets.MaxCarbs.Should().Be(new PlanTargetDto(30, "mode"));
        draft.Violations.Select(v => v.Rule).Should().NotContain(PlanRuleNames.CarbsOver);
        draft.Plan.CostCoverage.Should().BeGreaterThan(0);
        _fixture.Ai.LastCall.Messages.Last().Content.Should().Contain("At most 30 g carbs a day.");
    }

    // ---- helpers ----

    private async Task<(Guid Id, HttpClient Client)> StartAsync(bool pro = false)
    {
        await _fixture.ResetDatabaseAsync();
        _fixture.Ai.Reset();
        var person = await _world.PersonAsync("planner");
        if (pro) await _fixture.GrantProAsync(person.Id);
        return person;
    }

    private static GenerateMealPlanRequest Request(int durationDays = 2) => new()
    {
        StartDate = Monday,
        DurationDays = durationDays,
        Preference = "something simple",
    };

    private static async Task<GeneratedPlanDto> GenerateOneAsync(HttpClient client, int durationDays = 2)
    {
        var response = await client.PostAsJsonAsync("/api/MealPlans/generate", Request(durationDays));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<GenerateMealPlanResult>())!.Drafts.Single();
    }

    /// <summary>Two days in the meal_plan_v1 shape: 150 g then 100 g of the first food at breakfast, 200 g then 300 g of the second at lunch and dinner.</summary>
    private static string TwoDays(string first, string second) => $$"""
        {"name":"Two days","note":"Cheap and filling.","prepSessions":[],"days":[
          {"dayIndex":0,"entries":[
            {"slot":"BREAKFAST","kind":"food","foodName":"{{first}}","recipeId":null,"grams":150,"servings":null,"note":null,"prepSessionIndex":null},
            {"slot":"LUNCH","kind":"food","foodName":"{{second}}","recipeId":null,"grams":200,"servings":null,"note":null,"prepSessionIndex":null}]},
          {"dayIndex":1,"entries":[
            {"slot":"BREAKFAST","kind":"food","foodName":"{{first}}","recipeId":null,"grams":100,"servings":null,"note":null,"prepSessionIndex":null},
            {"slot":"DINNER","kind":"food","foodName":"{{second}}","recipeId":null,"grams":300,"servings":null,"note":null,"prepSessionIndex":null}]}
        ]}
        """;

    private async Task PriceAsync(Food food, decimal pricePerKg)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        db.FoodPrices.Add(new FoodPrice
        {
            Id = Guid.NewGuid(),
            FoodId = food.Id,
            RegionCode = PriceRegionDefaults.Code,
            Price = pricePerKg,
            Quantity = 1,
            Unit = PriceUnits.Kilogram,
            Confidence = PriceConfidence.Observed,
            ObservedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private Task<List<AiUsageLog>> LedgerAsync() =>
        _world.DbAsync(db => db.AiUsageLogs.AsNoTracking().OrderBy(l => l.CreatedAt).ToListAsync());
}
