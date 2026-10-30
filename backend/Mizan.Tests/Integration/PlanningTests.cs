using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Contracts.Planning;
using Mizan.Domain.Planning;
using Mizan.Infrastructure.Data;
using Xunit;
using static Mizan.Tests.Integration.PlanningWorld;

namespace Mizan.Tests.Integration;

/// <summary>A person plans a week, follows it, and shops for it. docs/PLANNING.md step 2.</summary>
[Collection("ApiIntegration")]
public class PlanningTests
{
    private readonly ApiTestFixture _fixture;
    private readonly PlanningWorld _world;

    public PlanningTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _world = new PlanningWorld(fixture);
    }

    [Fact]
    public async Task APersonBuildsAWeekFromRecipesFoodsAndNotes_AndSeesDayTotalsAndViolations()
    {
        await _fixture.ResetDatabaseAsync();
        var (me, client) = await _world.PersonAsync("planner");
        var rice = await _world.FoodAsync("Rice", 130, 2.7m, 28, 0.3m);
        var chicken = await _world.FoodAsync("Chicken", 165, 31, 0, 3.6m);
        var bread = await _world.FoodAsync("Bread", 250, 8, 50, 2);
        var chickenRice = await _world.RecipeAsync(me, "Chicken rice", 2, false, (rice, 200, "g", null), (chicken, 300, "g", null));

        var planId = await CreatePlanAsync(client, new CreateMealPlanRequest
        {
            Name = "Cutting week",
            StartDate = new DateOnly(2026, 10, 12),
            DurationDays = 7,
            Rules = new PlanRulesDto { MacroMode = "HighProtein", TargetCalories = 1500, TargetProtein = 100, ForbiddenFoodIds = [bread.Id] },
            PrepSessions = [new PrepSessionRequest { DayIndex = 0, Title = "Sunday cook" }],
            Entries =
            [
                RecipeEntry(chickenRice.Id, 1, day: 0, sessionIndex: 0),
                FoodEntry(bread.Id, 100, day: 0),
                TextEntry("Fruit from the market", day: 0),
                RecipeEntry(chickenRice.Id, 2, day: 3),
            ],
        });

        var plan = await PlanAsync(client, planId);
        plan.Status.Should().Be("Active");
        plan.EndDate.Should().Be(new DateOnly(2026, 10, 18));
        plan.Days.Should().HaveCount(7);
        plan.CanEdit.Should().BeTrue();
        plan.PrepSessions.Should().ContainSingle().Which.EntryCount.Should().Be(1);

        var day0 = plan.Days[0];
        day0.Date.Should().Be(new DateOnly(2026, 10, 12));
        day0.Slots.Select(s => s.Slot).Should().Equal("LUNCH", "DINNER", "SNACK");
        // Each slot carries its own total, so no surface adds entries up.
        day0.Slots.Select(s => s.Totals.Calories).Should().Equal(250m, 377.5m, 0m);
        // One serving of the recipe is half of 200 g rice and 300 g chicken; the bread is 100 g; the note has nothing to add.
        day0.Totals.Calories.Should().Be(377.5m + 250);
        day0.Totals.ProteinGrams.Should().Be(49.2m + 8);
        day0.Totals.CarbsGrams.Should().Be(28 + 50);
        day0.UnresolvedCount.Should().Be(1);
        Entries(day0).Single(e => e.Kind == "Text").Nutrition.Should().BeNull();
        Entries(day0).Single(e => e.Kind == "Recipe").Should().Match<PlanEntryDto>(e =>
            e.RecipeTitle == "Chicken rice" && e.PrepSessionId == plan.PrepSessions[0].Id && e.Nutrition!.Calories == 377.5m);
        day0.Violations.Select(v => v.Rule).Should().BeEquivalentTo(PlanRuleNames.ForbiddenFood, PlanRuleNames.Unresolved);
        day0.Violations.Single(v => v.Rule == PlanRuleNames.ForbiddenFood).Message.Should().Be("Bread is on the forbidden list.");
        plan.Days[3].Totals.Calories.Should().Be(755);
        plan.Days[3].Violations.Should().BeEmpty("98.4 g protein is within a tenth of the 100 g target");
        plan.Days[1].Violations.Select(v => v.Rule).Should().Equal([PlanRuleNames.ProteinUnder], "an empty day is under its protein target");

        // Entries are edited one at a time, by id.
        var textEntry = Entries(day0).Single(e => e.Kind == "Text");
        (await client.PutAsJsonAsync($"/api/MealPlans/{planId}/entries/{textEntry.Id}", FoodEntry(chicken.Id, 100, day: 0, slot: "SNACK")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var added = await client.PostAsJsonAsync($"/api/MealPlans/{planId}/entries", RecipeEntry(chickenRice.Id, 1, day: 6, slot: "BREAKFAST"));
        added.StatusCode.Should().Be(HttpStatusCode.Created);
        var addedId = (await added.Content.ReadFromJsonAsync<PlanEntryCreatedResult>())!.Id;
        (await client.DeleteAsync($"/api/MealPlans/{planId}/entries/{Entries(day0).Single(e => e.Kind == "Food").Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        plan = await PlanAsync(client, planId);
        plan.Days[0].UnresolvedCount.Should().Be(0);
        plan.Days[0].Violations.Select(v => v.Rule).Should().Equal([PlanRuleNames.ProteinUnder], "the forbidden bread is gone and every line resolves, but 80 g protein is under the target");
        plan.Days[6].Slots.Should().ContainSingle(s => s.Slot == "BREAKFAST").Which.Entries.Should().ContainSingle(e => e.Id == addedId);
        plan.EntryCount.Should().Be(4);

        // The plan cannot shrink past its last used day, and an entry cannot land outside it.
        var shrink = await client.PutAsJsonAsync($"/api/MealPlans/{planId}", new UpdateMealPlanRequest { Name = "Cutting week", StartDate = plan.StartDate, DurationDays = 5 });
        shrink.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync($"/api/MealPlans/{planId}/entries", FoodEntry(rice.Id, 100, day: 7)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync($"/api/MealPlans/{planId}/entries", new PlanEntryRequest { DayIndex = 0, Slot = "DINNER", Kind = "Recipe" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "a recipe entry needs a recipe and servings");

        // Rules are validated by the same code that evaluates them.
        var badRules = await client.PutAsJsonAsync($"/api/MealPlans/{planId}", new UpdateMealPlanRequest
        {
            Name = "Cutting week",
            StartDate = plan.StartDate,
            DurationDays = 7,
            Rules = new PlanRulesDto { WeeklyBudget = 500 },
        });
        badRules.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await badRules.Content.ReadAsStringAsync()).Should().Contain("A weekly budget needs a currency.");

        var list = await client.GetFromJsonAsync<PagedResultOf<MealPlanSummaryDto>>("/api/MealPlans");
        list!.Items.Should().ContainSingle(p => p.Id == planId).Which.Should().Match<MealPlanSummaryDto>(p => p.EntryCount == 4 && p.HasRules);
    }

    [Fact]
    public async Task LoggingAPlanEntry_WritesTheDiaryAndMarksItLogged()
    {
        await _fixture.ResetDatabaseAsync();
        var (me, client) = await _world.PersonAsync("eater");
        var rice = await _world.FoodAsync("Rice", 130, 2.7m, 28, 0.3m);
        var eggs = await _world.FoodAsync("Eggs", 155, 13, 1.1m, 11);
        var omelette = await _world.RecipeAsync(me, "Omelette", 2, false, (eggs, 200, "g", null));
        var start = new DateOnly(2026, 10, 12);

        var planId = await CreatePlanAsync(client, new CreateMealPlanRequest
        {
            Name = "Week",
            StartDate = start,
            DurationDays = 7,
            Entries = [FoodEntry(rice.Id, 150, day: 2, slot: "LUNCH"), RecipeEntry(omelette.Id, 1, day: 2, slot: "BREAKFAST"), TextEntry("Something", day: 2)],
        });
        var plan = await PlanAsync(client, planId);
        var day = plan.Days[2];
        var riceEntry = Entries(day).Single(e => e.Kind == "Food");
        var omeletteEntry = Entries(day).Single(e => e.Kind == "Recipe");
        var note = Entries(day).Single(e => e.Kind == "Text");
        plan.LoggedCount.Should().Be(0);

        var logged = await client.PostAsJsonAsync($"/api/MealPlans/{planId}/entries/{riceEntry.Id}/log", new LogPlanEntryRequest());
        logged.StatusCode.Should().Be(HttpStatusCode.OK, await logged.Content.ReadAsStringAsync());
        var result = await logged.Content.ReadFromJsonAsync<LogPlanEntryResultView>();
        result!.Date.Should().Be(start.AddDays(2));
        result.MealType.Should().Be("LUNCH");
        result.Calories.Should().Be(195);

        // The recipe is logged as its ingredient rows, each tied to the entry; the meal type can be overridden.
        (await client.PostAsJsonAsync($"/api/MealPlans/{planId}/entries/{omeletteEntry.Id}/log", new LogPlanEntryRequest { MealType = "DINNER" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var noteAttempt = await client.PostAsJsonAsync($"/api/MealPlans/{planId}/entries/{note.Id}/log", new LogPlanEntryRequest());
        noteAttempt.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await noteAttempt.Content.ReadAsStringAsync()).Should().Contain("no nutrition to log");

        var diary = await _fixture.GetFoodDiaryEntriesByUserId(me);
        diary.Should().HaveCount(2);
        diary.Should().OnlyContain(d => d.EntryDate == start.AddDays(2));
        diary.Single(d => d.PlanEntryId == riceEntry.Id).Should().Match<Mizan.Domain.Entities.FoodDiaryEntry>(d => d.FoodId == rice.Id && d.AmountGrams == 150 && d.MealType == "LUNCH");
        diary.Single(d => d.PlanEntryId == omeletteEntry.Id).Should().Match<Mizan.Domain.Entities.FoodDiaryEntry>(d => d.RecipeId == omelette.Id && d.AmountGrams == 100 && d.MealType == "DINNER");

        plan = await PlanAsync(client, planId);
        Entries(plan.Days[2]).Where(e => e.Kind != "Text").Should().OnlyContain(e => e.Logged);
        Entries(plan.Days[2]).Single(e => e.Kind == "Text").Logged.Should().BeFalse();
        plan.Days[2].LoggedCount.Should().Be(2);
        plan.LoggedCount.Should().Be(2);

        // A row logged on another date does not count as following the plan that day.
        (await client.PostAsJsonAsync($"/api/MealPlans/{planId}/entries/{riceEntry.Id}/log", new LogPlanEntryRequest { Date = start.AddDays(5) }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        plan = await PlanAsync(client, planId);
        plan.LoggedCount.Should().Be(2);
    }

    [Fact]
    public async Task GeneratingTheShoppingList_MergesEachItemAcrossRecipesAndPrepSessions_AndFlagsUnconvertibleLines()
    {
        await _fixture.ResetDatabaseAsync();
        var (me, client) = await _world.PersonAsync("shopper");
        var onion = await _world.FoodAsync("Onion", 40, 1.1m, 9, 0.1m);
        var flour = await _world.FoodAsync("Flour", 364, 10, 76, 1);
        var stew = await _world.RecipeAsync(me, "Stew", 2, false, (onion, 1, "kg", null), (null, null, null, "salt to taste"));
        var salad = await _world.RecipeAsync(me, "Salad", 1, false, (onion, 50, "g", null), (null, 1, "bunch", "fresh coriander"));
        var bread = await _world.RecipeAsync(me, "Bread", 2, false, (flour, 3, "cups", null));

        var planId = await CreatePlanAsync(client, new CreateMealPlanRequest
        {
            Name = "Week",
            StartDate = new DateOnly(2026, 10, 12),
            DurationDays = 7,
            PrepSessions = [new PrepSessionRequest { DayIndex = 0, Title = "Sunday batch" }],
            Entries =
            [
                RecipeEntry(stew.Id, 1, day: 0, sessionIndex: 0),
                RecipeEntry(salad.Id, 3, day: 1, sessionIndex: 0),
                FoodEntry(onion.Id, 100, day: 2),
                RecipeEntry(bread.Id, 4, day: 3),
                TextEntry("Injera from the shop", day: 4),
            ],
        });

        var generated = await client.PostAsJsonAsync($"/api/MealPlans/{planId}/shopping-list", new GenerateShoppingListRequest());
        generated.StatusCode.Should().Be(HttpStatusCode.Created, await generated.Content.ReadAsStringAsync());
        var result = (await generated.Content.ReadFromJsonAsync<GenerateShoppingListResult>())!;
        result.ItemCount.Should().Be(4);
        result.NeedsReviewCount.Should().Be(3);

        var list = await ListAsync(client, result.ListId);
        list.MealPlanId.Should().Be(planId);
        list.GeneratedAt.Should().NotBeNull();
        list.Name.Should().Be("Week shopping list");
        list.Items.Should().HaveCount(4);

        var sessionItems = list.Items.Where(i => i.PrepSessionTitle == "Sunday batch").ToList();
        sessionItems.Should().ContainSingle("only the coriander is bought for the batch session alone")
            .Which.Should().Match<ShoppingListItemDto>(i => i.FoodId == null && i.ItemName == "fresh coriander" && i.Amount == 3 && i.Unit == "bunch" && i.NeedsReview);
        list.Items.First().Should().BeEquivalentTo(sessionItems[0], "session lines are listed before the rest");

        var loose = list.Items.Where(i => i.PrepSessionId == null).ToList();
        loose.Single(i => i.FoodId == onion.Id).Grams.Should().Be(500 + 150 + 100,
            "half the stew's kilo, three salads' 50 g and the day's 100 g are one onion line, not one per session");
        loose.Single(i => i.FoodId == flour.Id).Should().Match<ShoppingListItemDto>(i => i.Grams == null && i.Amount == 6 && i.Unit == "cups" && i.NeedsReview);
        loose.Single(i => i.ItemName == "Injera from the shop").NeedsReview.Should().BeTrue();
        list.Items.Should().NotContain(i => i.ItemName.Contains("salt"));

        // Regenerating into the same list replaces its lines instead of piling up.
        var again = await client.PostAsJsonAsync($"/api/MealPlans/{planId}/shopping-list", new GenerateShoppingListRequest { ReplaceListId = result.ListId, Name = "Shop" });
        again.StatusCode.Should().Be(HttpStatusCode.Created);
        var replaced = await ListAsync(client, result.ListId);
        replaced.Name.Should().Be("Shop");
        replaced.Items.Should().HaveCount(4);

        // The list is then an ordinary list: edit, toggle, clear, delete.
        var flourItem = replaced.Items.Single(i => i.FoodId == flour.Id);
        (await client.PutAsJsonAsync($"/api/ShoppingLists/items/{flourItem.Id}", new UpdateShoppingListItemRequest { Grams = 750, NeedsReview = false }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PatchAsJsonAsync($"/api/ShoppingLists/items/{flourItem.Id}/toggle", new ToggleShoppingListItemRequest(true)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var cleared = await client.PostAsync($"/api/ShoppingLists/{result.ListId}/clear-checked", null);
        (await cleared.Content.ReadFromJsonAsync<ClearCheckedResult>())!.Removed.Should().Be(1);
        (await ListAsync(client, result.ListId)).Items.Should().HaveCount(3);
        (await client.DeleteAsync($"/api/ShoppingLists/{result.ListId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/ShoppingLists/{result.ListId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TodaysPlan_ReturnsTheActivePlanCoveringToday()
    {
        await _fixture.ResetDatabaseAsync();
        var (_, client) = await _world.PersonAsync("today");
        var rice = await _world.FoodAsync("Rice", 130, 2.7m, 28, 0.3m);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        (await client.GetAsync("/api/MealPlans/day")).StatusCode.Should().Be(HttpStatusCode.NotFound, "no plan yet");

        // Three days around today, so the user's time zone cannot move today out of it.
        var current = await CreatePlanAsync(client, new CreateMealPlanRequest
        {
            Name = "This week",
            StartDate = today.AddDays(-1),
            DurationDays = 3,
            Rules = new PlanRulesDto { EatingWindowStart = new TimeOnly(10, 0), EatingWindowEnd = new TimeOnly(20, 0) },
            Entries = [FoodEntry(rice.Id, 100, day: 0), FoodEntry(rice.Id, 200, day: 1), FoodEntry(rice.Id, 300, day: 2)],
        });
        var past = await CreatePlanAsync(client, new CreateMealPlanRequest { Name = "Last month", StartDate = today.AddDays(-40), DurationDays = 7 });

        var response = await client.GetAsync("/api/MealPlans/day");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var view = (await response.Content.ReadFromJsonAsync<PlanDayViewDto>())!;
        view.PlanId.Should().Be(current);
        view.Rules.EatingWindowEnd.Should().Be(new TimeOnly(20, 0));
        view.Day.Date.Should().Be(view.Date);
        view.Day.DayIndex.Should().BeInRange(0, 2);
        Entries(view.Day).Should().ContainSingle().Which.Grams.Should().Be(100 * (view.Day.DayIndex + 1));

        var dated = await client.GetFromJsonAsync<PlanDayViewDto>($"/api/MealPlans/day?date={today.AddDays(-1):yyyy-MM-dd}");
        dated!.Day.DayIndex.Should().Be(0);
        (await client.GetAsync($"/api/MealPlans/day?date={today.AddDays(-40):yyyy-MM-dd}")).StatusCode.Should().Be(HttpStatusCode.OK, "the older plan still covers that date");

        // An archived plan is not followed.
        (await client.PostAsJsonAsync($"/api/MealPlans/{current}/status", new SetMealPlanStatusRequest("Archived"))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/MealPlans/day")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Making a plan active again archives the other active personal plan.
        (await client.PostAsJsonAsync($"/api/MealPlans/{current}/status", new SetMealPlanStatusRequest("Active"))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PlanAsync(client, past)).Status.Should().Be("Archived");
        (await client.GetFromJsonAsync<PlanDayViewDto>("/api/MealPlans/day"))!.PlanId.Should().Be(current);
    }

    [Fact]
    public async Task CreatingAPlan_WithSomeoneElsesPrivateRecipe_IsRefusedAndSavesNothing()
    {
        await _fixture.ResetDatabaseAsync();
        var (me, client) = await _world.PersonAsync("plan");
        var (other, _) = await _world.PersonAsync("other");
        var mine = await _fixture.SeedRecipeAsync(me, "Mine", "d", 2, 10, false);
        var theirs = await _fixture.SeedRecipeAsync(other, "Theirs", "d", 2, 10, false);
        var shared = await _fixture.SeedRecipeAsync(other, "Shared", "d", 2, 10, true);

        CreateMealPlanRequest Plan(params Guid[] recipes) => new()
        {
            Name = "Week",
            StartDate = new DateOnly(2026, 10, 12),
            DurationDays = 7,
            Entries = recipes.Select(id => RecipeEntry(id, 1, day: 0)).ToList(),
        };

        var refused = await client.PostAsJsonAsync("/api/MealPlans", Plan(mine.Id, theirs.Id));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/MealPlans", Plan(mine.Id, shared.Id))).StatusCode.Should().Be(HttpStatusCode.Created);

        (await _world.DbAsync(db => db.MealPlans.CountAsync(p => p.UserId == me))).Should().Be(1);
        (await _world.DbAsync(db => db.PlanEntries.AnyAsync(e => e.RecipeId == theirs.Id))).Should().BeFalse();
    }

    [Fact]
    public async Task Migration_ConvertsLegacyPlanRecipesIntoEntries()
    {
        await _fixture.ResetDatabaseAsync();
        var (me, _) = await _world.PersonAsync("legacy");
        var recipe = await _fixture.SeedRecipeAsync(me, "Shiro", "d", 2, 10);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("RecipeNutritionSnapshots");

        var planId = Guid.NewGuid();
        var rowA = Guid.NewGuid();
        var rowB = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO meal_plans (id, user_id, name, start_date, end_date, created_at, updated_at) VALUES ({0}, {1}, 'Old week', '2026-09-07', '2026-09-13', NOW(), NOW())",
            planId, me);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO meal_plan_recipes (id, meal_plan_id, recipe_id, date, meal_type, servings) VALUES ({0}, {1}, {2}, '2026-09-07', 'dinner', 2), ({3}, {1}, {2}, '2026-09-10', 'lunch', 1.5)",
            rowA, planId, recipe.Id, rowB);

        await migrator.MigrateAsync();

        var plan = await db.MealPlans.AsNoTracking().Include(p => p.Entries).SingleAsync(p => p.Id == planId);
        plan.DurationDays.Should().Be(7);
        plan.StartDate.Should().Be(new DateOnly(2026, 9, 7));
        plan.Kind.Should().Be(PlanKind.Personal);
        plan.Status.Should().Be(PlanStatus.Active);
        plan.Entries.Should().HaveCount(2);
        plan.Entries.Single(e => e.Id == rowA).Should().Match<Mizan.Domain.Entities.PlanEntry>(e =>
            e.DayIndex == 0 && e.Slot == "DINNER" && e.Kind == PlanEntryKind.Recipe && e.RecipeId == recipe.Id && e.Servings == 2);
        plan.Entries.Single(e => e.Id == rowB).Should().Match<Mizan.Domain.Entities.PlanEntry>(e => e.DayIndex == 3 && e.Slot == "LUNCH" && e.Servings == 1.5m);
        (await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM information_schema.tables WHERE table_name LIKE '\\_legacy\\_%'").SingleAsync())
            .Should().Be(0, "the temporary copies are dropped");
    }

    [Fact]
    public async Task AKetoPlanWithNoTypedTargets_IsHeldToThirtyGramsOfCarbs_AndSaysTheGoalIsMissing()
    {
        await _fixture.ResetDatabaseAsync();
        var (_, client) = await _world.PersonAsync("keto");
        var bread = await _world.FoodAsync("Bread", 250, 8, 50, 2);

        var planId = await CreatePlanAsync(client, new CreateMealPlanRequest
        {
            Name = "Keto week",
            StartDate = new DateOnly(2026, 10, 12),
            DurationDays = 1,
            Rules = new PlanRulesDto { MacroMode = "Keto" },
            Entries = [FoodEntry(bread.Id, 100, day: 0)],
        });

        var plan = await PlanAsync(client, planId);
        plan.EffectiveTargets.MaxCarbs.Should().Be(new PlanTargetDto(30, "mode"));
        plan.EffectiveTargets.Calories.Value.Should().BeNull();
        plan.EffectiveTargets.GoalMissing.Should().BeTrue();
        plan.Days[0].Violations.Select(v => v.Rule).Should().Equal(PlanRuleNames.CarbsOver);

        var defaults = await client.GetFromJsonAsync<PlanTargetsDto>("/api/MealPlans/rules/defaults?macroMode=Keto&maxCarbs=40");
        defaults!.MaxCarbs.Should().Be(new PlanTargetDto(40, "typed"));
        defaults.GoalMissing.Should().BeTrue();
    }

    [Fact]
    public async Task AHighProteinPlanTakesItsTargetsFromTheGoal_AndFollowsTheGoalWhenItChanges()
    {
        await _fixture.ResetDatabaseAsync();
        var (_, client) = await _world.PersonAsync("lifter");
        var chicken = await _world.FoodAsync("Chicken", 165, 31, 0, 3.6m);
        (await client.PostAsJsonAsync("/api/Goals", new { GoalType = "general", TargetCalories = 2000, TargetProteinGrams = 100m }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var planId = await CreatePlanAsync(client, new CreateMealPlanRequest
        {
            Name = "Strength block",
            StartDate = new DateOnly(2026, 10, 12),
            DurationDays = 1,
            Rules = new PlanRulesDto { MacroMode = "HighProtein" },
            Entries = [FoodEntry(chicken.Id, 300, day: 0)],
        });

        var plan = await PlanAsync(client, planId);
        plan.EffectiveTargets.Calories.Should().Be(new PlanTargetDto(2000, "goal"));
        plan.EffectiveTargets.Protein.Should().Be(new PlanTargetDto(150, "derived"), "30% of 2000 kcal at 4 kcal a gram beats the goal's 100 g");
        plan.EffectiveTargets.GoalMissing.Should().BeFalse();
        plan.Days[0].Violations.Select(v => v.Rule).Should().Contain(PlanRuleNames.ProteinUnder, "300 g of chicken is 93 g protein");

        (await client.PostAsJsonAsync("/api/Goals", new { GoalType = "general", TargetCalories = 1200, TargetProteinGrams = 80m }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var after = await PlanAsync(client, planId);
        after.EffectiveTargets.Protein.Should().Be(new PlanTargetDto(90, "derived"));
        after.Days[0].Violations.Select(v => v.Rule).Should().NotContain(PlanRuleNames.ProteinUnder);

        var defaults = await client.GetFromJsonAsync<PlanTargetsDto>("/api/MealPlans/rules/defaults?macroMode=HighProtein");
        defaults!.Protein.Should().Be(new PlanTargetDto(90, "derived"));
    }

    private sealed record PagedResultOf<T>(List<T> Items, int TotalCount);

    private sealed record LogPlanEntryResultView(List<Guid> DiaryEntryIds, decimal Calories, DateOnly Date, string MealType);
}
