using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Billing;
using Mizan.Application.Commands;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Application.Queries;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The Pro catalogue an admin edits, what the pricing page reads back, and
/// what a subscriber can change from the billing page. Paddle is the fake in
/// <see cref="FakePaddleApiClient"/>; everything else is the real API on
/// PostgreSQL.
/// </summary>
[Collection("ApiIntegration")]
public class BillingTests(ApiTestFixture fixture)
{
    private async Task<HttpClient> ResetAndSignInAdminAsync()
    {
        await fixture.ResetDatabaseAsync();
        fixture.Paddle.Reset();
        using (var scope = fixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<HybridCache>().RemoveByTagAsync(CacheTags.BillingPlans);
        }

        var adminId = Guid.NewGuid();
        var email = $"billing-admin-{adminId:N}@example.com";
        await fixture.SeedUserAsync(adminId, email, role: "admin");
        return fixture.CreateAuthenticatedClient(adminId, email, role: "admin");
    }

    private async Task<(Guid UserId, HttpClient Client)> SignInUserAsync(string label)
    {
        var userId = Guid.NewGuid();
        var email = $"{label}-{userId:N}@example.com";
        await fixture.SeedUserAsync(userId, email);
        return (userId, fixture.CreateAuthenticatedClient(userId, email));
    }

    private static async Task<Guid> CreatePlanAsync(HttpClient admin, string name, string interval, int cents, int? trialDays = null)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/billing/plans", new { name, interval, amountCents = cents, trialDays });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CreateBillingPlanResult>())!.Id;
    }

    private async Task SubscribeAsync(Guid userId, string subscriptionId, string customerId, string priceId)
    {
        fixture.Paddle.SeedSubscription(subscriptionId, customerId, priceId);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var now = DateTime.UtcNow;
        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Plan = "pro",
            Status = "active",
            PaddleCustomerId = customerId,
            PaddleSubscriptionId = subscriptionId,
            PaddlePriceId = priceId,
            CurrentPeriodEnd = now.AddDays(30),
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AnAdminPlan_IsCreatedInPaddle_AndPublishedWithItsAutomaticDeal()
    {
        using var admin = await ResetAndSignInAdminAsync();
        using var anonymous = fixture.CreateClient();

        (await anonymous.GetFromJsonAsync<List<BillingPlanDto>>("/api/Subscriptions/plans")).Should().BeEmpty();

        var monthly = await CreatePlanAsync(admin, "Pro Monthly", "month", 299);
        var yearly = await CreatePlanAsync(admin, "Pro Yearly", "year", 2400, trialDays: 7);
        fixture.Paddle.Prices.Values.Select(p => (p.AmountCents, p.Interval, p.TrialDays))
            .Should().BeEquivalentTo(new[] { (299, (string?)"month", (int?)null), (2400, "year", 7) });

        // A deal on yearly only, and a code that must never be advertised.
        (await admin.PostAsJsonAsync("/api/admin/billing/discounts", new
        {
            label = "Launch offer", type = "percentage", amount = 25, recurring = false, planIds = new[] { yearly }
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsJsonAsync("/api/admin/billing/discounts", new
        {
            label = "Friends", code = "friends10", type = "percentage", amount = 90, recurring = true, maximumRecurringIntervals = 3
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var yearlyPriceId = fixture.Paddle.Prices.Values.Single(p => p.Interval == "year").Id;
        fixture.Paddle.Discounts.Values.Should().ContainSingle(d => d.Code == null)
            .Which.RestrictToPriceIds.Should().Equal(yearlyPriceId);
        fixture.Paddle.Discounts.Values.Should().ContainSingle(d => d.Code == "FRIENDS10");

        var plans = await anonymous.GetFromJsonAsync<List<BillingPlanDto>>("/api/Subscriptions/plans");
        plans!.Select(p => p.Id).Should().BeEquivalentTo(new[] { monthly, yearly });
        plans.Single(p => p.Id == monthly).Deal.Should().BeNull();
        var deal = plans.Single(p => p.Id == yearly).Deal;
        deal!.Label.Should().Be("Launch offer");
        deal.DiscountedAmountCents.Should().Be(1800);

        // Archiving takes it off sale in Paddle and on the pricing page.
        (await admin.PostAsync($"/api/admin/billing/plans/{yearly}/archive", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        fixture.Paddle.ArchivedPrices.Should().Contain(yearlyPriceId);
        (await anonymous.GetFromJsonAsync<List<BillingPlanDto>>("/api/Subscriptions/plans"))!
            .Select(p => p.Id).Should().Equal(monthly);
    }

    [Fact]
    public async Task ANewPrice_ArchivesTheOld_KeepsItsSubscribers_AndMovesItsDeal()
    {
        using var admin = await ResetAndSignInAdminAsync();
        var monthly = await CreatePlanAsync(admin, "Pro Monthly", "month", 199);
        var oldPriceId = fixture.Paddle.Prices.Values.Single().Id;
        (await admin.PostAsJsonAsync("/api/admin/billing/discounts", new
        {
            label = "Half off", type = "percentage", amount = 50, planIds = new[] { monthly }
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var (subscriberId, _) = await SignInUserAsync("existing");
        await SubscribeAsync(subscriberId, "sub_existing", "ctm_existing", oldPriceId);

        var response = await admin.PostAsJsonAsync($"/api/admin/billing/plans/{monthly}/price", new { id = monthly, amountCents = 299 });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var replacement = (await response.Content.ReadFromJsonAsync<CreateBillingPlanResult>())!.Id;

        fixture.Paddle.ArchivedPrices.Should().Equal(oldPriceId);
        var catalog = await admin.GetFromJsonAsync<AdminBillingCatalogDto>("/api/admin/billing");
        var old = catalog!.Plans.Single(p => p.Id == monthly);
        old.IsActive.Should().BeFalse();
        old.Subscribers.Should().Be(1);
        catalog.Plans.Single(p => p.Id == replacement).AmountCents.Should().Be(299);
        catalog.Discounts.Single().PlanIds.Should().BeEquivalentTo(new[] { monthly, replacement });

        var plans = await fixture.CreateClient().GetFromJsonAsync<List<BillingPlanDto>>("/api/Subscriptions/plans");
        plans!.Single().Deal!.DiscountedAmountCents.Should().Be(150);
    }

    [Fact]
    public async Task Import_AdoptsPaddlePricesOnce()
    {
        using var admin = await ResetAndSignInAdminAsync();
        fixture.Paddle.SeedPrice("Pro Monthly", "month", 299);
        fixture.Paddle.SeedPrice("Pro Yearly", "year", 2400);

        var first = await (await admin.PostAsync("/api/admin/billing/plans/import", null)).Content.ReadFromJsonAsync<ImportBillingPlansResult>();
        var second = await (await admin.PostAsync("/api/admin/billing/plans/import", null)).Content.ReadFromJsonAsync<ImportBillingPlansResult>();

        first!.Imported.Should().Be(2);
        second!.Imported.Should().Be(0);
    }

    [Fact]
    public async Task TheCatalogue_IsAdminOnly_AndRejectsBadInput()
    {
        using var admin = await ResetAndSignInAdminAsync();
        var (_, user) = await SignInUserAsync("not-admin");

        (await user.GetAsync("/api/admin/billing")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await user.PostAsJsonAsync("/api/admin/billing/plans", new { name = "Free money", interval = "month", amountCents = 100 }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await admin.PostAsJsonAsync("/api/admin/billing/plans", new { name = "Weekly", interval = "week", amountCents = 299 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/admin/billing/plans", new { name = "Too cheap", interval = "month", amountCents = 10 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/admin/billing/discounts", new { label = "Nope", type = "percentage", amount = 150 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/admin/billing/discounts", new { label = "Ghost", type = "percentage", amount = 10, planIds = new[] { Guid.NewGuid() } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await admin.PostAsJsonAsync("/api/admin/billing/discounts", new { label = "A", code = "SAME", type = "flat", amount = 100 }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsJsonAsync("/api/admin/billing/discounts", new { label = "B", code = "same", type = "flat", amount = 100 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        fixture.Paddle.Discounts.Should().HaveCount(1);

        fixture.Paddle.FailNext();
        (await admin.PostAsJsonAsync("/api/admin/billing/plans", new { name = "Pro", interval = "month", amountCents = 299 }))
            .StatusCode.Should().Be(HttpStatusCode.BadGateway);
        fixture.Paddle.Reset();
        fixture.Paddle.RefuseNext("price_amount_invalid", "Amount below the minimum.");
        var refused = await admin.PostAsJsonAsync("/api/admin/billing/plans", new { name = "Pro", interval = "month", amountCents = 299 });
        refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Amount below the minimum.");

        using var scope = fixture.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<MizanDbContext>().BillingPlans.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ASubscriber_SwitchesPlan_Cancels_AndKeepsTheSubscription()
    {
        using var admin = await ResetAndSignInAdminAsync();
        var monthly = await CreatePlanAsync(admin, "Pro Monthly", "month", 299);
        var yearly = await CreatePlanAsync(admin, "Pro Yearly", "year", 2400);
        var monthlyPrice = fixture.Paddle.Prices.Values.Single(p => p.Interval == "month").Id;

        var (userId, user) = await SignInUserAsync("subscriber");
        await SubscribeAsync(userId, "sub_self", "ctm_self", monthlyPrice);

        var me = await user.GetFromJsonAsync<MySubscriptionDto>("/api/Subscriptions/me");
        me!.PlanId.Should().Be(monthly);
        me.AmountCents.Should().Be(299);
        me.CanManage.Should().BeTrue();

        (await user.PostAsJsonAsync("/api/Subscriptions/change-plan", new { planId = monthly }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var preview = await (await user.PostAsJsonAsync("/api/Subscriptions/change-plan/preview", new { planId = yearly }))
            .Content.ReadFromJsonAsync<PlanChangePreviewDto>();
        preview!.DueNowCents.Should().Be(2400 - 299);
        preview.NextAmountCents.Should().Be(2400);
        fixture.Paddle.Subscriptions["sub_self"].PriceId.Should().Be(monthlyPrice, "a preview changes nothing");

        var switched = await (await user.PostAsJsonAsync("/api/Subscriptions/change-plan", new { planId = yearly }))
            .Content.ReadFromJsonAsync<MySubscriptionDto>();
        switched!.PlanId.Should().Be(yearly);
        switched.Interval.Should().Be("year");

        var canceled = await (await user.PostAsync("/api/Subscriptions/cancel", null)).Content.ReadFromJsonAsync<MySubscriptionDto>();
        canceled!.CancelsAt.Should().NotBeNull();
        canceled.IsPro.Should().BeTrue("a cancellation takes effect at the end of the paid period");
        (await user.PostAsJsonAsync("/api/Subscriptions/change-plan", new { planId = monthly }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var resumed = await (await user.PostAsync("/api/Subscriptions/resume", null)).Content.ReadFromJsonAsync<MySubscriptionDto>();
        resumed!.CancelsAt.Should().BeNull();
        fixture.Paddle.Subscriptions["sub_self"].ScheduledChangeAction.Should().BeNull();
    }

    [Fact]
    public async Task SelfService_RefusesAccountsWithNothingToManage_AndOtherCustomersInvoices()
    {
        await ResetAndSignInAdminAsync();
        var (_, free) = await SignInUserAsync("free");

        (await free.PostAsync("/api/Subscriptions/cancel", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await free.GetFromJsonAsync<List<BillingTransactionDto>>("/api/Subscriptions/transactions")).Should().BeEmpty();
        (await free.GetAsync("/api/Subscriptions/transactions/txn_x/invoice")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var (payerId, payer) = await SignInUserAsync("payer");
        await SubscribeAsync(payerId, "sub_payer", "ctm_payer", "pri_unlisted");
        fixture.Paddle.Transactions.Add(new PaddleTransaction("txn_ctm_payer_1", "completed", DateTime.UtcNow, 299, "USD", "2026-1"));

        (await payer.GetFromJsonAsync<List<BillingTransactionDto>>("/api/Subscriptions/transactions")).Should().ContainSingle();
        (await payer.GetFromJsonAsync<InvoiceLinkDto>("/api/Subscriptions/transactions/txn_ctm_payer_1/invoice"))!.Url.Should().EndWith(".pdf");
        (await payer.GetAsync("/api/Subscriptions/transactions/txn_ctm_other_1/invoice")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        fixture.Paddle.FailNext();
        (await payer.PostAsync("/api/Subscriptions/cancel", null)).StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task Webhooks_StoreScheduledCancellation_AndIgnoreOlderStates()
    {
        await ResetAndSignInAdminAsync();
        var (userId, user) = await SignInUserAsync("webhook");
        using var paddle = fixture.CreateClient();

        var t0 = DateTime.UtcNow.AddMinutes(-10);
        await PostEventAsync(paddle, userId, "subscription.created", t0, scheduledCancel: false);
        await PostEventAsync(paddle, userId, "subscription.updated", t0.AddMinutes(2), scheduledCancel: true);
        (await user.GetFromJsonAsync<MySubscriptionDto>("/api/Subscriptions/me"))!.CancelsAt.Should().NotBeNull();

        // Delivered late: the state from before the cancel was scheduled.
        await PostEventAsync(paddle, userId, "subscription.updated", t0.AddMinutes(1), scheduledCancel: false);
        var me = await user.GetFromJsonAsync<MySubscriptionDto>("/api/Subscriptions/me");
        me!.CancelsAt.Should().NotBeNull();
        me.IsPro.Should().BeTrue();

        // The cancel withdrawn later clears it.
        await PostEventAsync(paddle, userId, "subscription.updated", t0.AddMinutes(3), scheduledCancel: false);
        (await user.GetFromJsonAsync<MySubscriptionDto>("/api/Subscriptions/me"))!.CancelsAt.Should().BeNull();
    }

    [Fact]
    public async Task ProGates_FollowThePricingPage_AndAnswer402()
    {
        await ResetAndSignInAdminAsync();
        var (freeId, free) = await SignInUserAsync("gates");

        // Free: meal plans, shopping lists, and household invites are not capped.
        for (var i = 0; i < 2; i++)
        {
            (await free.PostAsJsonAsync("/api/MealPlans", new { name = $"Week {i}", startDate = "2030-01-01", endDate = "2030-01-07" }))
                .IsSuccessStatusCode.Should().BeTrue();
            (await free.PostAsJsonAsync("/api/ShoppingLists", new { name = $"List {i}" }))
                .IsSuccessStatusCode.Should().BeTrue();
        }

        // Pro: the Telegram bot, coach requests, and photo analysis.
        var telegram = await free.PostAsync("/api/Telegram/link", null);
        telegram.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        (await telegram.Content.ReadAsStringAsync()).Should().Contain("upgrade_required");

        var trainerId = Guid.NewGuid();
        await fixture.SeedUserAsync(trainerId, $"coach-{trainerId:N}@example.com", role: "trainer");
        (await free.PostAsJsonAsync("/api/Trainers/request", new { trainerId }))
            .StatusCode.Should().Be(HttpStatusCode.PaymentRequired);

        using var form = new MultipartFormDataContent { { new ByteArrayContent(new byte[] { 1, 2, 3 }), "image", "plate.jpg" } };
        var photo = await free.PostAsync("/api/Nutrition/ai/analyze-image", form);
        photo.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        (await photo.Content.ReadAsStringAsync()).Should().Contain("upgrade_required");

        await fixture.GrantProAsync(freeId);
        using (var scope = fixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IEntitlementService>().InvalidateAsync(freeId);
        }
        (await free.PostAsync("/api/Telegram/link", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await free.PostAsJsonAsync("/api/Trainers/request", new { trainerId })).IsSuccessStatusCode.Should().BeTrue();
    }

    private static async Task PostEventAsync(HttpClient paddle, Guid userId, string type, DateTime updatedAt, bool scheduledCancel)
    {
        var body = JsonSerializer.Serialize(new
        {
            event_id = $"evt_{Guid.NewGuid():N}",
            event_type = type,
            data = new
            {
                id = "sub_hook",
                customer_id = "ctm_hook",
                status = "active",
                custom_data = new { user_id = userId },
                items = new[] { new { price = new { id = "pri_hook" } } },
                current_billing_period = new { ends_at = "2030-01-01T00:00:00Z" },
                next_billed_at = scheduledCancel ? null : "2030-01-01T00:00:00Z",
                scheduled_change = scheduledCancel
                    ? new { action = "cancel", effective_at = "2030-01-01T00:00:00Z" }
                    : null,
                updated_at = updatedAt.ToString("O")
            }
        });

        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(ApiTestFixture.PaddleWebhookSecret));
        var signature = $"ts={ts};h1={Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{ts}:{body}")))}";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/paddle")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Paddle-Signature", signature);
        (await paddle.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
