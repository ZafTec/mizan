using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Contracts.Planning;
using Mizan.Contracts.Prices;
using Xunit;
using static Mizan.Tests.Integration.PlanningWorld;

namespace Mizan.Tests.Integration;

/// <summary>Prices: the worker feeds them, administrators curate them, people see and correct them. docs/PLANNING.md step 3.</summary>
[Collection("ApiIntegration")]
public class PricesTests
{
    private static readonly DateTime Seen = new(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

    private readonly ApiTestFixture _fixture;
    private readonly PlanningWorld _world;

    public PricesTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _world = new PlanningWorld(fixture);
    }

    [Fact]
    public async Task TheWorkerPostsObservations_AnAdminMapsTheQueuedProduct_AndTheNextObservationStoresAPrice()
    {
        await _fixture.ResetDatabaseAsync();
        var (_, admin) = await _world.PersonAsync("admin", role: "admin");
        var (_, person) = await _world.PersonAsync("shopper");
        var teff = await _world.FoodAsync("Teff", 367, 13, 73, 2.4m);
        var worker = Worker();

        var created = await admin.PostAsJsonAsync("/api/admin/prices/sources",
            new SavePriceSourceRequest("Arada Mart", "Scraper", "ET-AA", "https://aradamart.net", "aradamart", "0 3 * * *"));
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var sourceId = (await created.Content.ReadFromJsonAsync<PriceSourceCreatedResult>())!.Id;

        var sources = await worker.GetFromJsonAsync<List<PriceSourceDto>>("/api/internal/prices/sources");
        sources.Should().ContainSingle(s => s.Id == sourceId).Which.Should().Match<PriceSourceDto>(s =>
            s.ScraperKey == "aradamart" && s.Currency == "ETB" && s.Kind == "Scraper" && s.LastRunAt == null);

        var batch = new PriceObservationBatch(sourceId,
        [
            new PriceObservation("Teff Magna 1kg", "sku-teff", 185, "ETB", 1, "kg", Seen, "https://aradamart.net/teff"),
            new PriceObservation("Carrier bag", "sku-bag", 5, "ETB", null, null, Seen, null),
        ]);
        (await Receipt(worker, batch)).Should().Be(new PriceObservationReceipt(0, 2, 0), "nobody has said what either product is yet");

        var queue = await admin.GetFromJsonAsync<Paged<PriceSourceAliasDto>>($"/api/admin/prices/aliases?queue=true&sourceId={sourceId}");
        queue!.Items.Select(a => a.ProductName).Should().BeEquivalentTo("Teff Magna 1kg", "Carrier bag");
        var teffAlias = queue.Items.Single(a => a.ProductKey == "sku-teff");
        teffAlias.LastSeenPrice.Should().Be(185);
        var bagAlias = queue.Items.Single(a => a.ProductKey == "sku-bag");

        (await admin.PutAsJsonAsync($"/api/admin/prices/aliases/{teffAlias.Id}", new UpdatePriceAliasRequest(teff.Id, null, null)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.PutAsJsonAsync($"/api/admin/prices/aliases/{bagAlias.Id}", new UpdatePriceAliasRequest(null, null, null, Ignored: true)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetFromJsonAsync<Paged<PriceSourceAliasDto>>("/api/admin/prices/aliases?queue=true"))!.Items.Should().BeEmpty();

        // The shop renamed the product but kept its SKU: the key still finds the alias.
        var next = new PriceObservationBatch(sourceId,
        [
            new PriceObservation("Teff Magna 1 kg pack", "sku-teff", 190, "ETB", 1, "kg", Seen.AddDays(1), "https://aradamart.net/teff"),
            new PriceObservation("Carrier bag", "sku-bag", 5, "ETB", null, null, Seen.AddDays(1), null),
        ]);
        (await Receipt(worker, next)).Should().Be(new PriceObservationReceipt(1, 0, 1));

        (await worker.PostAsJsonAsync($"/api/internal/prices/sources/{sourceId}/runs",
                new PriceSourceRunResult(PriceRunStatus.Succeeded, null, 2, Seen.AddDays(1).AddMinutes(5))))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listed = (await admin.GetFromJsonAsync<List<PriceSourceAdminDto>>("/api/admin/prices/sources"))!.Single(s => s.Id == sourceId);
        listed.Should().Match<PriceSourceAdminDto>(s =>
            s.LastStatus == "succeeded" && s.LastObservations == 2 && s.AliasCount == 2 && s.UnresolvedCount == 0 && s.PriceCount == 1);

        var price = await person.GetFromJsonAsync<FoodPriceQuoteDto>($"/api/Foods/{teff.Id}/price");
        price.Should().Match<FoodPriceQuoteDto>(p =>
            p.Priced && p.Confidence == "Observed" && p.Currency == "ETB" && p.Price == 190 && p.Quantity == 1 && p.Unit == "kg"
            && p.PricePerKg == 190 && p.SourceName == "Arada Mart" && p.ObservedAt == Seen.AddDays(1));

        // Run now needs the worker's address; without it the API says so instead of failing somewhere else.
        var refused = await admin.PostAsync($"/api/admin/prices/sources/{sourceId}/run", null);
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Prices:WorkerUrl");
        _fixture.PricesWorker.IsConfigured = true;
        var started = await admin.PostAsync($"/api/admin/prices/sources/{sourceId}/run", null);
        started.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await started.Content.ReadFromJsonAsync<PriceSourceRunStartedDto>())!.Status.Should().Be("started");
        _fixture.PricesWorker.Runs.Should().Equal(sourceId);
    }

    [Fact]
    public async Task TheWorkerEndpoints_RefuseAnyoneButTheServiceActingForNobody()
    {
        await _fixture.ResetDatabaseAsync();
        var (userId, person) = await _world.PersonAsync("curious");
        var (_, admin) = await _world.PersonAsync("admin", role: "admin");
        var batch = new PriceObservationBatch(Guid.NewGuid(), []);

        (await _fixture.CreateClient().GetAsync("/api/internal/prices/sources")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await person.GetAsync("/api/internal/prices/sources")).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "a session cookie is not a service key");
        (await admin.PostAsJsonAsync("/api/internal/prices/observations", batch)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var wrongKey = _fixture.CreateClient();
        wrongKey.DefaultRequestHeaders.Add("X-Api-Key", "not-the-key");
        (await wrongKey.GetAsync("/api/internal/prices/sources")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var impersonating = Worker();
        impersonating.DefaultRequestHeaders.Add("X-Impersonate-User", userId.ToString());
        (await impersonating.GetAsync("/api/internal/prices/sources")).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the service acting for a user is that user, and users do not feed the price book");

        (await Worker().GetAsync("/api/internal/prices/sources")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await person.GetAsync("/api/admin/prices/sources")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task APersonSeesAnEstimateOnTheFoodPage_ReportsAWrongPrice_AndTheAcceptedReportWins()
    {
        await _fixture.ResetDatabaseAsync();
        var (adminId, admin) = await _world.PersonAsync("admin", role: "admin");
        var (personId, person) = await _world.PersonAsync("shopper");
        var onion = await _world.FoodAsync("Onion", 40, 1.1m, 9, 0.1m);
        var garlic = await _world.FoodAsync("Garlic", 149, 6.4m, 33, 0.5m);
        var unpriced = await _world.FoodAsync("Dragon fruit", 60, 1.2m, 13, 0);

        var seeded = await admin.PostAsync("/api/admin/prices/seed-staples", null);
        seeded.StatusCode.Should().Be(HttpStatusCode.OK, await seeded.Content.ReadAsStringAsync());
        var seed = (await seeded.Content.ReadFromJsonAsync<SeedStaplePricesResult>())!;
        seed.Stored.Should().Be(2, "onion and garlic are in the catalogue; the other staples are not");
        seed.Missing.Should().Contain("Teff").And.NotContain("Onion");
        (await (await admin.PostAsync("/api/admin/prices/seed-staples", null)).Content.ReadFromJsonAsync<SeedStaplePricesResult>())!
            .Should().Match<SeedStaplePricesResult>(r => r.Stored == 0 && r.AlreadyPriced == 2, "seeding twice changes nothing");

        var shown = await person.GetFromJsonAsync<FoodPriceQuoteDto>($"/api/Foods/{onion.Id}/price");
        shown.Should().Match<FoodPriceQuoteDto>(p =>
            p.Priced && p.Confidence == "Estimated" && p.Price == 70 && p.Unit == "kg" && p.Currency == "ETB" && p.SourceName == "Manual");
        (await person.GetFromJsonAsync<FoodPriceQuoteDto>($"/api/Foods/{unpriced.Id}/price"))
            .Should().Match<FoodPriceQuoteDto>(p => !p.Priced && p.Price == null && p.Confidence == null && p.Currency == "ETB");

        // A retry with the same key is the same report.
        var report = new CreatePriceReportRequest(45, 500, "g", "Shola market, Tuesday");
        Guid reportId = Guid.Empty;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/Foods/{onion.Id}/price-reports") { Content = JsonContent.Create(report) };
            request.Headers.Add("Idempotency-Key", "report-onion-1");
            var response = await person.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            var id = (await response.Content.ReadFromJsonAsync<PriceReportCreatedResult>())!.Id;
            if (attempt == 0) reportId = id;
            else id.Should().Be(reportId);
        }
        (await person.PostAsJsonAsync($"/api/Foods/{onion.Id}/price-reports", new CreatePriceReportRequest(45, 1, "bunch")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await _world.DbAsync(db => db.Notifications.Where(n => n.UserId == adminId && n.Type == "price_report").ToListAsync()))
            .Should().ContainSingle().Which.Should().Match<Mizan.Domain.Entities.Notification>(n =>
                n.Title == "Price report for Onion" && n.LinkUrl == "/admin/prices#reports", "the row opens the reports on the prices page");

        var open = await admin.GetFromJsonAsync<Paged<PriceReportDto>>("/api/admin/prices/reports");
        open!.Items.Should().ContainSingle().Which.Should().Match<PriceReportDto>(r =>
            r.Id == reportId && r.FoodName == "Onion" && r.ShownPricePerKg == 70 && r.SuggestedPricePerKg == 90 && r.Status == "Open" && r.Currency == "ETB");

        (await admin.PostAsJsonAsync($"/api/admin/prices/reports/{reportId}/accept", new ResolvePriceReportRequest("Matches the market")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.PostAsJsonAsync($"/api/admin/prices/reports/{reportId}/accept", new ResolvePriceReportRequest()))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "a report is resolved once");

        // A newer estimate does not displace a reported price.
        (await admin.PostAsJsonAsync("/api/admin/prices/prices", new CreateManualPriceRequest(onion.Id, "ET-AA", 60, 1, "kg")))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var corrected = await person.GetFromJsonAsync<FoodPriceQuoteDto>($"/api/Foods/{onion.Id}/price");
        corrected.Should().Match<FoodPriceQuoteDto>(p =>
            p.Confidence == "Reported" && p.Price == 45 && p.Quantity == 500 && p.Unit == "g" && p.PricePerKg == 90 && p.SourceName == "User reports");

        // An observed price, typed in by an administrator who saw it on a shelf, wins over the report.
        (await admin.PostAsJsonAsync("/api/admin/prices/prices", new CreateManualPriceRequest(onion.Id, "ET-AA", 80, 1, "kg", "Observed")))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await person.GetFromJsonAsync<FoodPriceQuoteDto>($"/api/Foods/{onion.Id}/price"))!.Confidence.Should().Be("Observed");

        (await _world.DbAsync(db => db.Notifications.Where(n => n.UserId == personId && n.Type == "price_report_resolved").ToListAsync()))
            .Should().ContainSingle().Which.Title.Should().Contain("accepted");
        (await admin.GetFromJsonAsync<Paged<PriceReportDto>>("/api/admin/prices/reports"))!.Items.Should().BeEmpty();
        (await admin.GetFromJsonAsync<Paged<PriceReportDto>>("/api/admin/prices/reports?status=Accepted"))!.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task ThePlanAndItsShoppingList_ShowWhatTheyCost_AndWhatIsNotPriced()
    {
        await _fixture.ResetDatabaseAsync();
        var (_, admin) = await _world.PersonAsync("admin", role: "admin");
        var (me, client) = await _world.PersonAsync("planner");
        var rice = await _world.FoodAsync("Rice", 130, 2.7m, 28, 0.3m);
        var chicken = await _world.FoodAsync("Chicken", 165, 31, 0, 3.6m);
        var salt = await _world.FoodAsync("Salt", 0, 0, 0, 0);
        await ManualPriceAsync(admin, rice.Id, 160, 1, "kg", "Observed");
        await ManualPriceAsync(admin, chicken.Id, 650, 1, "kg", "Estimated");
        var chickenRice = await _world.RecipeAsync(me, "Chicken rice", 2, false, (rice, 200, "g", null), (chicken, 300, "g", null));

        var planId = await CreatePlanAsync(client, new CreateMealPlanRequest
        {
            Name = "Week",
            StartDate = new DateOnly(2026, 10, 12),
            DurationDays = 7,
            Rules = new PlanRulesDto { WeeklyBudget = 300, DailyBudget = 150, Currency = "ETB" },
            Entries =
            [
                RecipeEntry(chickenRice.Id, 1, day: 0),
                FoodEntry(salt.Id, 5, day: 0, slot: "DINNER"),
                RecipeEntry(chickenRice.Id, 2, day: 3),
                TextEntry("Fruit", day: 4),
            ],
        });

        var plan = await PlanAsync(client, planId);
        plan.Currency.Should().Be("ETB");
        // One serving is 100 g rice at 0.16 and 150 g chicken at 0.65.
        Entries(plan.Days[0]).Single(e => e.Kind == "Recipe").Cost.Should().Be(16 + 97.5m);
        Entries(plan.Days[0]).Single(e => e.Kind == "Food").Cost.Should().BeNull("salt has no price in the region");
        plan.Days[0].Cost.Should().Be(113.5m);
        plan.Days[3].Cost.Should().Be(227);
        plan.Days[1].Cost.Should().BeNull("nothing is planned that day");
        plan.Days[4].Cost.Should().BeNull("a note is never priced");
        // The daily budget marks the day over it, and only that day.
        plan.Days.Select(d => d.OverBudget).Should().Equal(false, null, null, true, null, null, null);
        plan.Days[3].Violations.Should().ContainSingle(v => v.Rule == "daily_budget_over");
        plan.TotalCost.Should().Be(340.5m);
        plan.WeeklyCost.Should().Be(340.5m, "the plan is a week long");
        plan.OverBudget.Should().BeTrue();
        plan.CostConfidence.Should().Be("Estimated", "the chicken price is an estimate");
        plan.CostCoverage.Should().Be(0.5m, "rice and chicken are priced; salt and the note are not");
        plan.UnpricedItems.Select(i => i.Name).Should().BeEquivalentTo("Salt", "Fruit");

        var day = await client.GetFromJsonAsync<PlanDayViewDto>("/api/MealPlans/day?date=2026-10-15");
        day!.Currency.Should().Be("ETB");
        day.Day.Cost.Should().Be(227);

        var generated = await client.PostAsJsonAsync($"/api/MealPlans/{planId}/shopping-list", new GenerateShoppingListRequest());
        var listId = (await generated.Content.ReadFromJsonAsync<GenerateShoppingListResult>())!.ListId;
        var list = await ListAsync(client, listId);
        list.Currency.Should().Be("ETB");
        list.Items.Single(i => i.FoodId == rice.Id).Should().Match<ShoppingListItemDto>(i => i.Grams == 300 && i.Cost == 48 && i.CostConfidence == "Observed");
        list.Items.Single(i => i.FoodId == chicken.Id).Should().Match<ShoppingListItemDto>(i => i.Cost == 292.5m && i.CostConfidence == "Estimated");
        list.Items.Single(i => i.FoodId == salt.Id).Should().Match<ShoppingListItemDto>(i => i.Cost == null && i.UnpricedReason != null);
        list.TotalCost.Should().Be(340.5m, "the list and the plan cost the same");
        list.UnpricedCount.Should().Be(2);
        list.CostConfidence.Should().Be("Estimated");

        // A new price reaches the cached plan and list.
        await ManualPriceAsync(admin, salt.Id, 30, 1, "kg", "Observed");
        (await PlanAsync(client, planId)).Days[0].Cost.Should().Be(113.65m);
        (await ListAsync(client, listId)).UnpricedCount.Should().Be(1);
    }

    [Fact]
    public async Task SwitchingRegion_PricesThePlanInThatRegionsCurrency()
    {
        await _fixture.ResetDatabaseAsync();
        var (_, admin) = await _world.PersonAsync("admin", role: "admin");
        var (_, client) = await _world.PersonAsync("traveller");
        var rice = await _world.FoodAsync("Rice", 130, 2.7m, 28, 0.3m);
        await ManualPriceAsync(admin, rice.Id, 160, 1, "kg", "Observed");

        (await admin.PostAsJsonAsync("/api/admin/prices/regions", new CreatePriceRegionRequest("ke-nb", "Nairobi", "kes")))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await admin.PostAsJsonAsync("/api/admin/prices/regions", new CreatePriceRegionRequest("KE-MB", "Mombasa", "KES", Enabled: false)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await admin.PostAsJsonAsync("/api/admin/prices/prices", new CreateManualPriceRequest(rice.Id, "KE-NB", 250, 2, "kg", "Observed")))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.GetFromJsonAsync<List<PriceRegionDto>>("/api/Prices/regions"))!.Select(r => r.Code)
            .Should().BeEquivalentTo(["ET-AA", "KE-NB"], "a disabled region is not offered");

        var planId = await CreatePlanAsync(client, new CreateMealPlanRequest
        {
            Name = "Week",
            StartDate = new DateOnly(2026, 10, 12),
            DurationDays = 14,
            Entries = [FoodEntry(rice.Id, 500, day: 0)],
        });
        var plan = await PlanAsync(client, planId);
        plan.Should().Match<MealPlanDetailDto>(p => p.Currency == "ETB" && p.TotalCost == 80 && p.WeeklyCost == 40);

        (await client.PutAsJsonAsync("/api/Users/me", new { priceRegionCode = "KE-MB" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync("/api/Users/me", new { priceRegionCode = "XX-YY" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync("/api/Users/me", new { priceRegionCode = "KE-NB" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        plan = await PlanAsync(client, planId);
        plan.Should().Match<MealPlanDetailDto>(p => p.Currency == "KES" && p.TotalCost == 62.5m && p.CostConfidence == "Observed");
        (await client.GetFromJsonAsync<FoodPriceQuoteDto>($"/api/Foods/{rice.Id}/price"))
            .Should().Match<FoodPriceQuoteDto>(p => p.RegionCode == "KE-NB" && p.Currency == "KES" && p.PricePerKg == 125);
        (await client.GetStringAsync("/api/Users/me")).Should().Contain("\"priceRegionCode\":\"KE-NB\"");

        // When an administrator turns the region off, its people fall back to the default region.
        (await admin.PutAsJsonAsync("/api/admin/prices/regions/KE-NB", new UpdatePriceRegionRequest("Nairobi", "KES", Enabled: false)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await PlanAsync(client, planId)).Currency.Should().Be("ETB");
    }

    [Fact]
    public async Task AnAdminImportsTheStarterFoodsTwice_AndTheCatalogueHasEachOnce()
    {
        await _fixture.ResetDatabaseAsync();
        var (_, admin) = await _world.PersonAsync("admin", role: "admin");
        var (_, person) = await _world.PersonAsync("person");
        await _world.FoodAsync("Butter", 700, 1, 0, 80);

        var first = await admin.PostAsync("/api/admin/prices/import-starter-foods", null);
        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        var imported = (await first.Content.ReadFromJsonAsync<ImportStarterFoodsResult>())!;
        imported.Existing.Should().Be(1, "the catalogue already had a shared Butter");
        imported.CreatedNames.Should().Contain(["Injera", "Teff", "Berbere", "Niter kibbeh", "Lentils", "Shiro wat"]).And.NotContain("Butter");

        var again = (await (await admin.PostAsync("/api/admin/prices/import-starter-foods", null)).Content.ReadFromJsonAsync<ImportStarterFoodsResult>())!;
        again.Should().Match<ImportStarterFoodsResult>(r => r.Created == 0 && r.Existing == imported.Created + 1);

        var foods = await _world.DbAsync(db => db.Foods.AsNoTracking().Where(f => f.UserId == null).ToListAsync());
        foods.GroupBy(f => f.Name.ToLower()).Should().OnlyContain(g => g.Count() == 1);
        // The ids are the ones PostgreSQL derives, as the catalogue seeds derive theirs.
        var injeraId = await _world.DbAsync(db => db.Database.SqlQueryRaw<Guid>("SELECT md5('mizan-food:Injera')::uuid AS \"Value\"").SingleAsync());
        foods.Single(f => f.Name == "Injera").Should().Match<Domain.Entities.Food>(f => f.Id == injeraId && !f.IsVerified && f.CaloriesPer100g > 0);
        foods.Single(f => f.Name == "Teff").IsVerified.Should().BeTrue("USDA values are sourced");

        (await person.PostAsync("/api/admin/prices/import-starter-foods", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TheReviewQueueSuggestsAFoodAndPackageSize_AndAnAdminAcceptsThemInBulk()
    {
        await _fixture.ResetDatabaseAsync();
        var (_, admin) = await _world.PersonAsync("admin", role: "admin");
        var teff = await _world.FoodAsync("Teff", 367, 13, 73, 2.4m);
        var milk = await _world.FoodAsync("Whole milk", 61, 3.2m, 4.8m, 3.3m);
        var eggs = await _world.FoodAsync("Eggs", 143, 12.6m, 0.7m, 9.5m);
        var created = await admin.PostAsJsonAsync("/api/admin/prices/sources",
            new SavePriceSourceRequest("Arada Mart", "Scraper", "ET-AA", "https://aradamart.net", "aradamart", "0 3 * * *"));
        var sourceId = (await created.Content.ReadFromJsonAsync<PriceSourceCreatedResult>())!.Id;
        await Receipt(Worker(), new PriceObservationBatch(sourceId,
        [
            new PriceObservation("Teff Magna 500gm", "sku-teff", 95, "ETB", null, null, Seen, null),
            new PriceObservation("Whole Milk 1L", "sku-milk", 90, "ETB", null, null, Seen, null),
            new PriceObservation("Farm eggs 6pc", "sku-eggs", 80, "ETB", null, null, Seen, null),
            new PriceObservation("Carrier bag", "sku-bag", 5, "ETB", null, null, Seen, null),
        ]));

        var queue = (await admin.GetFromJsonAsync<Paged<PriceSourceAliasDto>>("/api/admin/prices/aliases?queue=true"))!.Items;
        queue.Should().HaveCount(4);
        queue.Single(a => a.ProductName == "Teff Magna 500gm").Should().Match<PriceSourceAliasDto>(a =>
            a.SuggestedFoodId == teff.Id && a.SuggestedFoodName == "Teff" && a.SuggestedQuantity == 500 && a.SuggestedUnit == "g");
        queue.Single(a => a.ProductName == "Whole Milk 1L").Should().Match<PriceSourceAliasDto>(a =>
            a.SuggestedFoodId == milk.Id && a.SuggestedQuantity == 1 && a.SuggestedUnit == "l");
        queue.Single(a => a.ProductName == "Farm eggs 6pc").Should().Match<PriceSourceAliasDto>(a =>
            a.SuggestedFoodId == eggs.Id && a.SuggestedQuantity == 6 && a.SuggestedUnit == "piece");
        queue.Single(a => a.ProductName == "Carrier bag").SuggestedFoodId.Should().BeNull();

        var response = await admin.PostAsJsonAsync("/api/admin/prices/aliases/accept-suggestions",
            new AcceptPriceAliasSuggestionsRequest([.. queue.Select(a => a.Id), Guid.NewGuid()]));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var result = (await response.Content.ReadFromJsonAsync<AcceptPriceAliasSuggestionsResult>())!;
        result.Accepted.Select(a => a.FoodId).Should().BeEquivalentTo([teff.Id, milk.Id, eggs.Id]);
        result.Skipped.Select(s => s.Reason).Should().BeEquivalentTo(["no_suggestion", "not_found"]);
        var left = (await admin.GetFromJsonAsync<Paged<PriceSourceAliasDto>>("/api/admin/prices/aliases?queue=true"))!.Items;
        left.Should().ContainSingle().Which.ProductName.Should().Be("Carrier bag");
        var teffAlias = (await admin.GetFromJsonAsync<Paged<PriceSourceAliasDto>>("/api/admin/prices/aliases?search=teff"))!.Items.Single();
        teffAlias.Should().Match<PriceSourceAliasDto>(a => a.FoodId == teff.Id && a.Quantity == 500 && a.Unit == "g" && a.SuggestedFoodId == null);

        // Accepting again changes nothing: they are reviewed now.
        var again = (await (await admin.PostAsJsonAsync("/api/admin/prices/aliases/accept-suggestions",
            new AcceptPriceAliasSuggestionsRequest([teffAlias.Id]))).Content.ReadFromJsonAsync<AcceptPriceAliasSuggestionsResult>())!;
        again.Skipped.Should().ContainSingle().Which.Reason.Should().Be("already_reviewed");
    }

    private HttpClient Worker()
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
        return client;
    }

    private static async Task<PriceObservationReceipt> Receipt(HttpClient worker, PriceObservationBatch batch)
    {
        var response = await worker.PostAsJsonAsync("/api/internal/prices/observations", batch);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PriceObservationReceipt>())!;
    }

    private static async Task ManualPriceAsync(HttpClient admin, Guid foodId, decimal price, decimal quantity, string unit, string confidence)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/prices/prices", new CreateManualPriceRequest(foodId, "ET-AA", price, quantity, unit, confidence));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    private sealed record Paged<T>(List<T> Items, int TotalCount);
}
