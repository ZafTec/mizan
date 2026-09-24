using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Commands;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// Owner-only household deletion: permission, the other-member guard, explicit
/// consent for shared plans, stale confirmations, and the exact scope of what
/// is removed.
/// </summary>
[Collection("ApiIntegration")]
public class HouseholdDeletionTests(ApiTestFixture fixture)
{
    private sealed record Seeded(Guid HouseholdId, Guid OwnerId, HttpClient Owner);

    private async Task<Seeded> SeedHouseholdAsync(string ownerRole = "admin")
    {
        await fixture.ResetDatabaseAsync();
        var ownerId = Guid.NewGuid();
        var email = $"owner-{ownerId:N}@example.com";
        await fixture.SeedUserAsync(ownerId, email);
        var householdId = Guid.NewGuid();
        await WithDbAsync(async db =>
        {
            db.Households.Add(new Household { Id = householdId, Name = "Home", CreatedBy = ownerId, CreatedAt = DateTime.UtcNow });
            db.HouseholdMembers.Add(new HouseholdMember { HouseholdId = householdId, UserId = ownerId, Role = ownerRole, JoinedAt = DateTime.UtcNow });
            db.UserHouseholdPreferences.Add(new UserHouseholdPreference { UserId = ownerId, ActiveHouseholdId = householdId, UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });
        return new Seeded(householdId, ownerId, fixture.CreateAuthenticatedClient(ownerId, email));
    }

    private async Task<(Guid Id, HttpClient Client)> AddMemberAsync(Guid householdId, string role)
    {
        var id = Guid.NewGuid();
        var email = $"member-{id:N}@example.com";
        await fixture.SeedUserAsync(id, email);
        await WithDbAsync(async db =>
        {
            db.HouseholdMembers.Add(new HouseholdMember { HouseholdId = householdId, UserId = id, Role = role, JoinedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });
        return (id, fixture.CreateAuthenticatedClient(id, email));
    }

    private async Task WithDbAsync(Func<MizanDbContext, Task> action)
    {
        using var scope = fixture.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<MizanDbContext>());
    }

    private static async Task<HouseholdDeletionResult> PreviewAsync(HttpClient client, Guid householdId) =>
        (await client.GetFromJsonAsync<HouseholdDeletionResult>($"/api/Households/{householdId}/deletion"))!;

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, Guid householdId, string version, bool deletePlans = false) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/Households/{householdId}")
        {
            Content = JsonContent.Create(new { version, deletePlans })
        });

    [Fact]
    public async Task OnlyTheOwnerMayPreviewOrDelete()
    {
        var home = await SeedHouseholdAsync();
        var (_, admin) = await AddMemberAsync(home.HouseholdId, "admin");
        var outsiderId = Guid.NewGuid();
        await fixture.SeedUserAsync(outsiderId, $"outsider-{outsiderId:N}@example.com");
        using var outsider = fixture.CreateAuthenticatedClient(outsiderId, $"outsider-{outsiderId:N}@example.com");
        using var anonymous = fixture.CreateClient();

        (await admin.GetAsync($"/api/Households/{home.HouseholdId}/deletion")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var refused = await DeleteAsync(admin, home.HouseholdId, "anything", deletePlans: true);
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadFromJsonAsync<HouseholdDeletionResult>())!.Message.Should().Contain("owner");
        (await DeleteAsync(outsider, home.HouseholdId, "anything")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DeleteAsync(anonymous, home.HouseholdId, "anything")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await WithDbAsync(async db => (await db.Households.AnyAsync(h => h.Id == home.HouseholdId)).Should().BeTrue());
    }

    [Fact]
    public async Task AnExplicitOwnerRoleOutranksTheCreator()
    {
        var home = await SeedHouseholdAsync();
        var (_, owner) = await AddMemberAsync(home.HouseholdId, "owner");

        (await home.Owner.GetAsync($"/api/Households/{home.HouseholdId}/deletion")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await owner.GetAsync($"/api/Households/{home.HouseholdId}/deletion")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnOwnerWithOtherMembersIsBlocked()
    {
        var home = await SeedHouseholdAsync();
        await AddMemberAsync(home.HouseholdId, "member");

        var preview = await PreviewAsync(home.Owner, home.HouseholdId);
        preview.Status.Should().Be(HouseholdDeletionStatus.HasOtherMembers);
        preview.Preview!.OtherMemberCount.Should().Be(1);

        var refused = await DeleteAsync(home.Owner, home.HouseholdId, preview.Preview.Version, deletePlans: true);
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await refused.Content.ReadFromJsonAsync<HouseholdDeletionResult>())!.Status.Should().Be(HouseholdDeletionStatus.HasOtherMembers);
    }

    [Fact]
    public async Task ASoleOwnerWithoutPlansDeletesAfterConfirmation_AndARetryIsHarmless()
    {
        var home = await SeedHouseholdAsync();

        var preview = await PreviewAsync(home.Owner, home.HouseholdId);
        preview.Status.Should().Be(HouseholdDeletionStatus.Ready);
        preview.Preview!.HouseholdName.Should().Be("Home");
        preview.Preview.ShoppingListCount.Should().Be(0);
        preview.Preview.MealPlanCount.Should().Be(0);

        (await DeleteAsync(home.Owner, home.HouseholdId, preview.Preview.Version)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await DeleteAsync(home.Owner, home.HouseholdId, preview.Preview.Version)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        await WithDbAsync(async db =>
        {
            (await db.Households.AnyAsync(h => h.Id == home.HouseholdId)).Should().BeFalse();
            (await db.UserHouseholdPreferences.SingleAsync(p => p.UserId == home.OwnerId)).ActiveHouseholdId.Should().BeNull();
        });
    }

    [Fact]
    public async Task PlansAreDeletedOnlyWithConsent_AndNothingOutsideTheHouseholdIsTouched()
    {
        var home = await SeedHouseholdAsync();
        var otherHouseholdId = Guid.NewGuid();
        Guid sharedList = Guid.NewGuid(), personalList = Guid.NewGuid(), otherList = Guid.NewGuid();
        Guid sharedPlan = Guid.NewGuid(), personalPlan = Guid.NewGuid();
        var recipe = await fixture.SeedRecipeAsync(home.OwnerId, "Shared stew", "", 2, 10);
        await WithDbAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            db.Households.Add(new Household { Id = otherHouseholdId, Name = "Elsewhere", CreatedBy = home.OwnerId, CreatedAt = now });
            db.ShoppingLists.AddRange(
                new ShoppingList { Id = sharedList, UserId = home.OwnerId, HouseholdId = home.HouseholdId, Name = "Shared", CreatedAt = now, UpdatedAt = now },
                new ShoppingList { Id = personalList, UserId = home.OwnerId, Name = "Mine", CreatedAt = now, UpdatedAt = now },
                new ShoppingList { Id = otherList, UserId = home.OwnerId, HouseholdId = otherHouseholdId, Name = "Theirs", CreatedAt = now, UpdatedAt = now });
            db.ShoppingListItems.AddRange(
                new ShoppingListItem { Id = Guid.NewGuid(), ShoppingListId = sharedList, ItemName = "Lentils" },
                new ShoppingListItem { Id = Guid.NewGuid(), ShoppingListId = sharedList, ItemName = "Onions" },
                new ShoppingListItem { Id = Guid.NewGuid(), ShoppingListId = personalList, ItemName = "Coffee" });
            db.MealPlans.AddRange(
                new MealPlan { Id = sharedPlan, UserId = home.OwnerId, HouseholdId = home.HouseholdId, StartDate = today.AddDays(-30), EndDate = today.AddDays(-24), CreatedAt = now, UpdatedAt = now },
                new MealPlan { Id = personalPlan, UserId = home.OwnerId, StartDate = today, EndDate = today.AddDays(6), CreatedAt = now, UpdatedAt = now });
            db.MealPlanRecipes.AddRange(
                new MealPlanRecipe { Id = Guid.NewGuid(), MealPlanId = sharedPlan, RecipeId = recipe.Id, Date = today.AddDays(-30) },
                new MealPlanRecipe { Id = Guid.NewGuid(), MealPlanId = personalPlan, RecipeId = recipe.Id, Date = today });
            var tracked = await db.Recipes.SingleAsync(r => r.Id == recipe.Id);
            tracked.HouseholdId = home.HouseholdId;
            await db.SaveChangesAsync();
        });

        var preview = (await PreviewAsync(home.Owner, home.HouseholdId)).Preview!;
        preview.ShoppingListCount.Should().Be(1);
        preview.ShoppingListItemCount.Should().Be(2);
        preview.MealPlanCount.Should().Be(1, "a past plan is disclosed, never treated as disposable");
        preview.MealPlanRecipeCount.Should().Be(1);

        var withoutConsent = await DeleteAsync(home.Owner, home.HouseholdId, preview.Version);
        withoutConsent.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await withoutConsent.Content.ReadFromJsonAsync<HouseholdDeletionResult>())!.Status.Should().Be(HouseholdDeletionStatus.PlansNotConfirmed);
        await WithDbAsync(async db => (await db.ShoppingLists.AnyAsync(l => l.Id == sharedList)).Should().BeTrue());

        (await DeleteAsync(home.Owner, home.HouseholdId, preview.Version, deletePlans: true)).StatusCode.Should().Be(HttpStatusCode.OK);

        await WithDbAsync(async db =>
        {
            (await db.ShoppingLists.Select(l => l.Id).ToListAsync()).Should().BeEquivalentTo([personalList, otherList]);
            (await db.ShoppingListItems.Select(i => i.ItemName).ToListAsync()).Should().Equal("Coffee");
            (await db.MealPlans.Select(p => p.Id).ToListAsync()).Should().Equal(personalPlan);
            (await db.MealPlanRecipes.CountAsync()).Should().Be(1);
            var kept = await db.Recipes.SingleAsync(r => r.Id == recipe.Id);
            kept.HouseholdId.Should().BeNull();
            kept.UserId.Should().Be(home.OwnerId);
            (await db.Households.Select(h => h.Id).ToListAsync()).Should().Equal(otherHouseholdId);
        });
    }

    [Fact]
    public async Task AConfirmationMadeBeforeAChangeIsRejectedWithFreshCounts()
    {
        var home = await SeedHouseholdAsync();
        var preview = (await PreviewAsync(home.Owner, home.HouseholdId)).Preview!;

        await WithDbAsync(async db =>
        {
            var now = DateTime.UtcNow;
            db.ShoppingLists.Add(new ShoppingList { Id = Guid.NewGuid(), UserId = home.OwnerId, HouseholdId = home.HouseholdId, Name = "New", CreatedAt = now, UpdatedAt = now });
            await db.SaveChangesAsync();
        });
        var stale = await DeleteAsync(home.Owner, home.HouseholdId, preview.Version, deletePlans: true);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = (await stale.Content.ReadFromJsonAsync<HouseholdDeletionResult>())!;
        body.Status.Should().Be(HouseholdDeletionStatus.Stale);
        body.Preview!.ShoppingListCount.Should().Be(1);

        await AddMemberAsync(home.HouseholdId, "member");
        var joined = await DeleteAsync(home.Owner, home.HouseholdId, body.Preview.Version, deletePlans: true);
        joined.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await joined.Content.ReadFromJsonAsync<HouseholdDeletionResult>())!.Status.Should().Be(HouseholdDeletionStatus.HasOtherMembers);
        await WithDbAsync(async db => (await db.Households.AnyAsync(h => h.Id == home.HouseholdId)).Should().BeTrue());
    }

    [Fact]
    public async Task ConcurrentConfirmationsDeleteOnce()
    {
        var home = await SeedHouseholdAsync();
        var version = (await PreviewAsync(home.Owner, home.HouseholdId)).Preview!.Version;
        using var second = fixture.CreateAuthenticatedClient(home.OwnerId, $"owner-{home.OwnerId:N}@example.com");

        var responses = await Task.WhenAll(
            DeleteAsync(home.Owner, home.HouseholdId, version),
            DeleteAsync(second, home.HouseholdId, version));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.NotFound]);
    }
}
