using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Contracts.Planning;
using Xunit;
using static Mizan.Tests.Integration.PlanningWorld;

namespace Mizan.Tests.Integration;

/// <summary>Plans that more than one person touches: a household, a template, a coach. docs/PLANNING.md step 2.</summary>
[Collection("ApiIntegration")]
public class PlanningSharingTests
{
    private readonly ApiTestFixture _fixture;
    private readonly PlanningWorld _world;

    public PlanningSharingTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _world = new PlanningWorld(fixture);
    }

    [Fact]
    public async Task AHouseholdMemberSeesTheSharedPlan_ButOnlyAnEditorChangesTheList()
    {
        await _fixture.ResetDatabaseAsync();
        var (owner, ownerClient) = await _world.PersonAsync("owner");
        var (reader, readerClient) = await _world.PersonAsync("reader");
        var (editor, editorClient) = await _world.PersonAsync("editor");
        var (_, strangerClient) = await _world.PersonAsync("stranger");
        var household = await _world.HouseholdAsync(owner, (reader, false), (editor, true));
        var onion = await _world.FoodAsync("Onion", 40, 1.1m, 9, 0.1m);
        var stew = await _world.RecipeAsync(owner, "Stew", 2, false, (onion, 500, "g", null));
        await _world.DbAsync(async db =>
        {
            var tracked = await db.Recipes.SingleAsync(r => r.Id == stew.Id);
            tracked.HouseholdId = household;
            return await db.SaveChangesAsync();
        });

        var planId = await CreatePlanAsync(ownerClient, new CreateMealPlanRequest
        {
            Name = "Family week",
            StartDate = new DateOnly(2026, 10, 12),
            DurationDays = 7,
            HouseholdId = household,
            Entries = [RecipeEntry(stew.Id, 2, day: 0)],
        });
        var generated = await ownerClient.PostAsJsonAsync($"/api/MealPlans/{planId}/shopping-list", new GenerateShoppingListRequest());
        var listId = (await generated.Content.ReadFromJsonAsync<GenerateShoppingListResult>())!.ListId;

        // Members see the plan and the list; a stranger sees neither.
        var seen = await PlanAsync(readerClient, planId);
        seen.HouseholdId.Should().Be(household);
        (await readerClient.GetFromJsonAsync<PagedOf<MealPlanSummaryDto>>("/api/MealPlans"))!.Items.Should().ContainSingle(p => p.Id == planId);
        var list = await ListAsync(readerClient, listId);
        list.HouseholdId.Should().Be(household);
        list.CanEdit.Should().BeFalse();
        (await ListAsync(editorClient, listId)).CanEdit.Should().BeTrue();
        (await readerClient.GetFromJsonAsync<PagedOf<ShoppingListSummaryDto>>("/api/ShoppingLists"))!.Items.Should().ContainSingle(l => l.Id == listId);
        (await strangerClient.GetAsync($"/api/MealPlans/{planId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await strangerClient.GetAsync($"/api/ShoppingLists/{listId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // A member logs the household recipe from the shared plan.
        var entry = Entries(seen.Days[0]).Single();
        (await readerClient.PostAsJsonAsync($"/api/MealPlans/{planId}/entries/{entry.Id}/log", new LogPlanEntryRequest()))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _fixture.GetFoodDiaryEntriesByUserId(reader)).Should().ContainSingle(d => d.PlanEntryId == entry.Id);

        // Only the owner and a member allowed to edit lists write to it.
        var item = new AddShoppingListItemRequest("Coffee");
        (await readerClient.PostAsJsonAsync($"/api/ShoppingLists/{listId}/items", item)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await editorClient.PostAsJsonAsync($"/api/ShoppingLists/{listId}/items", item)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ownerClient.PostAsJsonAsync($"/api/ShoppingLists/{listId}/items", item)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await strangerClient.PostAsJsonAsync($"/api/ShoppingLists/{listId}/items", item)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var onionLine = list.Items.Single(i => i.FoodId == onion.Id);
        (await readerClient.PatchAsJsonAsync($"/api/ShoppingLists/items/{onionLine.Id}/toggle", new ToggleShoppingListItemRequest(true))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await editorClient.PatchAsJsonAsync($"/api/ShoppingLists/items/{onionLine.Id}/toggle", new ToggleShoppingListItemRequest(true))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await readerClient.PutAsJsonAsync($"/api/ShoppingLists/{listId}", new UpdateShoppingListRequest("Renamed"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await readerClient.DeleteAsync($"/api/ShoppingLists/{listId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await editorClient.PutAsJsonAsync($"/api/ShoppingLists/{listId}", new UpdateShoppingListRequest("Renamed"))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await ListAsync(ownerClient, listId);
        after.Name.Should().Be("Renamed");
        after.Items.Should().HaveCount(3);
        after.Items.Single(i => i.FoodId == onion.Id).IsChecked.Should().BeTrue();

        // A list filed under a household nobody else belongs to is refused on creation.
        (await strangerClient.PostAsJsonAsync("/api/ShoppingLists", new CreateShoppingListRequest("Sneaky", household))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnAdminPublishesATemplate_AndAnyoneAppliesIt()
    {
        await _fixture.ResetDatabaseAsync();
        var (_, admin) = await _world.PersonAsync("admin", role: "admin");
        var (_, coach) = await _world.PersonAsync("coach", role: "trainer");
        var (me, client) = await _world.PersonAsync("member");
        var oats = await _world.FoodAsync("Oats", 389, 17, 66, 7);
        var lentils = await _world.FoodAsync("Lentils", 116, 9, 20, 0.4m);
        var publicRecipe = await _world.RecipeAsync(me, "Lentil pot", 4, true, (lentils, 400, "g", null));

        var template = new CreateTemplateRequest
        {
            Name = "Keto starter",
            DurationDays = 3,
            Global = true,
            Rules = new PlanRulesDto { MacroMode = "Keto", TargetCalories = 1800 },
            PrepSessions = [new PrepSessionRequest { DayIndex = 0, Title = "Batch day" }],
            Entries = [FoodEntry(oats.Id, 60, day: 0, slot: "BREAKFAST"), RecipeEntry(publicRecipe.Id, 1, day: 1, sessionIndex: 0)],
        };

        // Only an administrator publishes for everyone; a coach keeps their own; a plain user keeps none.
        (await client.PostAsJsonAsync("/api/MealPlans/templates", template)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await coach.PostAsJsonAsync("/api/MealPlans/templates", template)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var coachOwn = await coach.PostAsJsonAsync("/api/MealPlans/templates", template with { Name = "Coach's own", Global = false });
        coachOwn.StatusCode.Should().Be(HttpStatusCode.Created, await coachOwn.Content.ReadAsStringAsync());
        var published = await admin.PostAsJsonAsync("/api/MealPlans/templates", template);
        published.StatusCode.Should().Be(HttpStatusCode.Created, await published.Content.ReadAsStringAsync());
        var templateId = (await published.Content.ReadFromJsonAsync<MealPlanCreatedResult>())!.Id;

        var visible = await client.GetFromJsonAsync<List<TemplateDto>>("/api/MealPlans/templates");
        visible!.Should().ContainSingle().Which.Should().Match<TemplateDto>(t => t.Id == templateId && t.Global && t.EntryCount == 2 && t.Rules.MacroMode == "Keto");
        (await coach.GetFromJsonAsync<List<TemplateDto>>("/api/MealPlans/templates"))!.Should().HaveCount(2, "the coach sees the global one and their own");
        (await client.GetFromJsonAsync<PagedOf<MealPlanSummaryDto>>("/api/MealPlans"))!.Items.Should().BeEmpty("templates are not plans");

        // The admin changes it; a user cannot.
        (await client.PutAsJsonAsync($"/api/MealPlans/templates/{templateId}", new UpdateTemplateRequest { Name = "Mine now", DurationDays = 3 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PutAsJsonAsync($"/api/MealPlans/templates/{templateId}", new UpdateTemplateRequest { Name = "Keto starter, week one", DurationDays = 3, Rules = template.Rules })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.PostAsJsonAsync($"/api/MealPlans/{templateId}/entries", TextEntry("Water, two litres", day: 2))).StatusCode.Should().Be(HttpStatusCode.Created);

        // Anyone applies it and gets their own active plan with the entries re-pointed at the copied session.
        var applied = await client.PostAsJsonAsync($"/api/MealPlans/templates/{templateId}/apply", new ApplyTemplateRequest { StartDate = new DateOnly(2026, 11, 2) });
        applied.StatusCode.Should().Be(HttpStatusCode.Created, await applied.Content.ReadAsStringAsync());
        var planId = (await applied.Content.ReadFromJsonAsync<MealPlanCreatedResult>())!.Id;
        var plan = await PlanAsync(client, planId);
        plan.Kind.Should().Be("Personal");
        plan.Status.Should().Be("Active");
        plan.Name.Should().Be("Keto starter, week one");
        plan.UserId.Should().Be(me);
        plan.StartDate.Should().Be(new DateOnly(2026, 11, 2));
        plan.EndDate.Should().Be(new DateOnly(2026, 11, 4));
        plan.Rules.MacroMode.Should().Be("Keto");
        plan.EntryCount.Should().Be(3);
        plan.PrepSessions.Should().ContainSingle().Which.Id.Should().NotBe((await PlanAsync(admin, templateId)).PrepSessions.Single().Id);
        Entries(plan.Days[1]).Single().PrepSessionId.Should().Be(plan.PrepSessions.Single().Id);
        plan.Days[0].Violations.Select(v => v.Rule).Should().Contain("carbs_over", "60 g of oats is over the keto ceiling of 30 g carbs");

        // The copy is independent of the template.
        (await admin.DeleteAsync($"/api/MealPlans/templates/{templateId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/MealPlans/{planId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetFromJsonAsync<List<TemplateDto>>("/api/MealPlans/templates"))!.Should().BeEmpty();
    }

    [Fact]
    public async Task ACoachDraftsAPlan_TheClientAcceptsIt_AndTheOldActivePlanIsArchived()
    {
        await _fixture.ResetDatabaseAsync();
        var (coachId, coach) = await _world.PersonAsync("coach", role: "trainer");
        var (clientId, client) = await _world.PersonAsync("client");
        await _world.CoachingAsync(coachId, clientId, canPlanMeals: true);
        var rice = await _world.FoodAsync("Rice", 130, 2.7m, 28, 0.3m);
        var coachRecipe = await _world.RecipeAsync(coachId, "Coach's bowl", 1, false, (rice, 150, "g", null));

        var oldPlan = await CreatePlanAsync(client, new CreateMealPlanRequest { Name = "My own", StartDate = new DateOnly(2026, 10, 12), DurationDays = 7 });

        var drafted = await coach.PostAsJsonAsync($"/api/Trainers/clients/{clientId}/meal-plans", new CreateMealPlanRequest
        {
            Name = "Coach's week",
            StartDate = new DateOnly(2026, 10, 19),
            DurationDays = 7,
            Rules = new PlanRulesDto { TargetProtein = 140 },
            Entries = [RecipeEntry(coachRecipe.Id, 1, day: 0)],
        });
        drafted.StatusCode.Should().Be(HttpStatusCode.Created, await drafted.Content.ReadAsStringAsync());
        var draftId = (await drafted.Content.ReadFromJsonAsync<MealPlanCreatedResult>())!.Id;

        // Both see the draft; the client is told.
        var draft = await PlanAsync(client, draftId);
        draft.Status.Should().Be("Draft");
        draft.UserId.Should().Be(clientId);
        draft.AuthoredByUserId.Should().Be(coachId);
        draft.CanEdit.Should().BeFalse("the client decides, the coach edits");
        (await PlanAsync(coach, draftId)).CanEdit.Should().BeTrue();
        (await coach.GetFromJsonAsync<PagedOf<MealPlanSummaryDto>>("/api/MealPlans"))!.Items.Should().ContainSingle(p => p.Id == draftId);
        (await client.GetFromJsonAsync<PagedOf<MealPlanSummaryDto>>("/api/MealPlans"))!.Items.Select(p => p.Id).Should().BeEquivalentTo([oldPlan, draftId]);
        var clientNotifications = await client.GetFromJsonAsync<NotificationsOf>("/api/Notifications");
        clientNotifications!.Items.Should().ContainSingle(n => n.Type == "meal_plan_drafted" && n.LinkUrl == $"/meal-plan/{draftId}");

        // While it is a draft, the coach edits it and the client does not.
        (await coach.PostAsJsonAsync($"/api/MealPlans/{draftId}/entries", FoodEntry(rice.Id, 100, day: 1))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.PostAsJsonAsync($"/api/MealPlans/{draftId}/entries", FoodEntry(rice.Id, 100, day: 2))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync($"/api/MealPlans/{draftId}/status", new SetMealPlanStatusRequest("Active"))).StatusCode.Should().Be(HttpStatusCode.Forbidden, "a draft is accepted, not switched on");
        (await coach.PostAsync($"/api/MealPlans/{draftId}/accept", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "only the client decides");

        // The client logs the coach's private recipe from the draft once it is theirs.
        (await client.PostAsync($"/api/MealPlans/{draftId}/accept", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var accepted = await PlanAsync(client, draftId);
        accepted.Status.Should().Be("Active");
        accepted.CanEdit.Should().BeTrue();
        accepted.EntryCount.Should().Be(2);
        (await PlanAsync(client, oldPlan)).Status.Should().Be("Archived");
        (await coach.PostAsJsonAsync($"/api/MealPlans/{draftId}/entries", FoodEntry(rice.Id, 100, day: 3))).StatusCode.Should().Be(HttpStatusCode.Forbidden, "the coach's say ends with the draft");
        (await coach.GetFromJsonAsync<NotificationsOf>("/api/Notifications"))!.Items.Should().ContainSingle(n => n.Type == "meal_plan_accepted" && n.LinkUrl == $"/trainer/clients/{clientId}/plan");
        var entry = Entries(accepted.Days[0]).Single();
        (await client.PostAsJsonAsync($"/api/MealPlans/{draftId}/entries/{entry.Id}/log", new LogPlanEntryRequest())).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"/api/MealPlans/{draftId}/accept", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest, "it is no longer a draft");

        // A second draft can be declined.
        var second = await coach.PostAsJsonAsync($"/api/Trainers/clients/{clientId}/meal-plans", new CreateMealPlanRequest { Name = "Another", StartDate = new DateOnly(2026, 10, 26), DurationDays = 7 });
        var secondId = (await second.Content.ReadFromJsonAsync<MealPlanCreatedResult>())!.Id;
        (await client.PostAsync($"/api/MealPlans/{secondId}/decline", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PlanAsync(client, secondId)).Status.Should().Be("Archived");
        (await PlanAsync(client, draftId)).Status.Should().Be("Active", "declining leaves the accepted plan alone");
        (await coach.GetFromJsonAsync<NotificationsOf>("/api/Notifications"))!.Items.Should().ContainSingle(n => n.Type == "meal_plan_declined");
    }

    [Fact]
    public async Task ACoachKeepsATemplate_AndDraftsAClientsPlanFromIt()
    {
        await _fixture.ResetDatabaseAsync();
        var (coachId, coach) = await _world.PersonAsync("coach", role: "trainer");
        var (otherCoachId, otherCoach) = await _world.PersonAsync("other-coach", role: "trainer");
        var (clientId, client) = await _world.PersonAsync("client");
        var (otherClientId, _) = await _world.PersonAsync("other-client");
        await _world.CoachingAsync(coachId, clientId, canPlanMeals: true);
        await _world.CoachingAsync(otherCoachId, otherClientId, canPlanMeals: true);
        var eggs = await _world.FoodAsync("Eggs", 143, 13, 1, 10);

        var created = await coach.PostAsJsonAsync("/api/MealPlans/templates", new CreateTemplateRequest
        {
            Name = "Cut, week one",
            DurationDays = 2,
            Rules = new PlanRulesDto { MacroMode = "HighProtein", TargetProtein = 150 },
            PrepSessions = [new PrepSessionRequest { DayIndex = 0, Title = "Sunday batch" }],
            Entries = [FoodEntry(eggs.Id, 120, day: 0, slot: "BREAKFAST") with { PrepSessionIndex = 0 }, TextEntry("Grilled fish", day: 1)],
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var templateId = (await created.Content.ReadFromJsonAsync<MealPlanCreatedResult>())!.Id;

        // The template is the coach's alone, and they change it like a plan.
        (await coach.PutAsJsonAsync($"/api/MealPlans/templates/{templateId}", new UpdateTemplateRequest { Name = "Cut, week one (eggs)", DurationDays = 2 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await coach.GetFromJsonAsync<List<TemplateDto>>("/api/MealPlans/templates"))!.Should().ContainSingle()
            .Which.Should().Match<TemplateDto>(t => t.Id == templateId && !t.Global && t.OwnerId == coachId && t.EntryCount == 2);
        (await otherCoach.GetFromJsonAsync<List<TemplateDto>>("/api/MealPlans/templates"))!.Should().BeEmpty();
        (await client.GetFromJsonAsync<List<TemplateDto>>("/api/MealPlans/templates"))!.Should().BeEmpty();
        (await otherCoach.DeleteAsync($"/api/MealPlans/templates/{templateId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Another coach cannot draft from it, and a client is not a coach.
        var apply = new ApplyTemplateForClientRequest { TemplateId = templateId, StartDate = new DateOnly(2026, 11, 2) };
        (await otherCoach.PostAsJsonAsync($"/api/Trainers/clients/{otherClientId}/meal-plans/from-template", apply))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "the template is not theirs");
        (await client.PostAsJsonAsync($"/api/Trainers/clients/{clientId}/meal-plans/from-template", apply))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // The coach drafts the client's plan from it: the client's draft, with the sessions copied.
        var drafted = await coach.PostAsJsonAsync($"/api/Trainers/clients/{clientId}/meal-plans/from-template", apply);
        drafted.StatusCode.Should().Be(HttpStatusCode.Created, await drafted.Content.ReadAsStringAsync());
        var draftId = (await drafted.Content.ReadFromJsonAsync<MealPlanCreatedResult>())!.Id;
        var draft = await PlanAsync(client, draftId);
        draft.Kind.Should().Be("Personal");
        draft.Status.Should().Be("Draft");
        draft.UserId.Should().Be(clientId);
        draft.AuthoredByUserId.Should().Be(coachId);
        draft.Name.Should().Be("Cut, week one (eggs)");
        draft.StartDate.Should().Be(new DateOnly(2026, 11, 2));
        draft.Rules.TargetProtein.Should().Be(150);
        draft.EntryCount.Should().Be(2);
        Entries(draft.Days[0]).Single().PrepSessionId.Should().Be(draft.PrepSessions.Single().Id);
        (await client.GetFromJsonAsync<NotificationsOf>("/api/Notifications"))!.Items
            .Should().ContainSingle(n => n.Type == "meal_plan_drafted" && n.LinkUrl == $"/meal-plan/{draftId}");

        // Deleting the template leaves the draft alone.
        (await coach.DeleteAsync($"/api/MealPlans/templates/{templateId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/MealPlans/{draftId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await coach.GetFromJsonAsync<List<TemplateDto>>("/api/MealPlans/templates"))!.Should().BeEmpty();
    }

    [Fact]
    public async Task ACoachWithoutTheGrant_IsRefused()
    {
        await _fixture.ResetDatabaseAsync();
        var (coachId, coach) = await _world.PersonAsync("coach", role: "trainer");
        var (otherCoachId, otherCoach) = await _world.PersonAsync("other-coach", role: "trainer");
        var (clientId, client) = await _world.PersonAsync("client");
        var (_, plainUser) = await _world.PersonAsync("plain");
        await _world.CoachingAsync(coachId, clientId, canPlanMeals: false);
        var body = new CreateMealPlanRequest { Name = "Week", StartDate = new DateOnly(2026, 10, 19), DurationDays = 7 };

        (await coach.PostAsJsonAsync($"/api/Trainers/clients/{clientId}/meal-plans", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "no CanPlanMeals grant");
        (await otherCoach.PostAsJsonAsync($"/api/Trainers/clients/{clientId}/meal-plans", body)).StatusCode.Should().Be(HttpStatusCode.NotFound, "no relationship at all");
        (await plainUser.PostAsJsonAsync($"/api/Trainers/clients/{clientId}/meal-plans", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "not a trainer");

        // The client grants it like the other grants, and the draft goes through.
        var relationshipId = await _world.DbAsync(db => db.TrainerClientRelationships.Where(r => r.TrainerId == coachId).Select(r => r.Id).SingleAsync());
        (await client.PatchAsJsonAsync($"/api/Trainers/relationships/{relationshipId}/grants", new { canPlanMeals = true })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var trainer = await client.GetFromJsonAsync<MyTrainerView>("/api/Trainers/my-trainer");
        trainer!.CanPlanMeals.Should().BeTrue();
        (await coach.PostAsJsonAsync($"/api/Trainers/clients/{clientId}/meal-plans", body)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await _world.DbAsync(db => db.MealPlans.CountAsync(p => p.UserId == clientId))).Should().Be(1, "the refused attempts saved nothing");
    }

    private sealed record PagedOf<T>(List<T> Items, int TotalCount);

    private sealed record NotificationsOf(List<NotificationView> Items);

    private sealed record NotificationView(Guid Id, string Type, string Title, string? LinkUrl);

    private sealed record MyTrainerView(Guid RelationshipId, bool CanPlanMeals);
}
