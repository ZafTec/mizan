using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Common;
using Mizan.Application.Queries;
using Mizan.Contracts.Admin;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>Household search, the household detail, and support notes, through the API (docs/ADMIN.md#households-users-and-support-notes).</summary>
[Collection("ApiIntegration")]
public class HouseholdSupportStoriesTests(ApiTestFixture fixture)
{
    [Fact]
    public async Task HouseholdSearchFindsAHouseholdByAMembersEmail_AndTheDetailShowsMembersOwnerAndPlans()
    {
        await fixture.ResetDatabaseAsync();
        using var admin = await AdminAsync("search");
        var (household, owner, member) = await SeedHouseholdAsync("Blue door", $"owner-{Guid.NewGuid():N}@example.test", "lidya.blue@example.test");
        await SeedHouseholdAsync("Green gate", $"owner-{Guid.NewGuid():N}@example.test", "someone.else@example.test");

        var found = await admin.GetFromJsonAsync<PagedResult<AdminHouseholdSummary>>("/api/Households/admin/all?search=LIDYA.BLUE");
        found!.Items.Should().ContainSingle().Which.Name.Should().Be("Blue door");
        (await admin.GetFromJsonAsync<PagedResult<AdminHouseholdSummary>>("/api/Households/admin/all?search=green"))!
            .Items.Should().ContainSingle().Which.Name.Should().Be("Green gate");
        (await admin.GetFromJsonAsync<PagedResult<AdminHouseholdSummary>>("/api/Households/admin/all?sortBy=name&sortOrder=asc&pageSize=1"))!
            .Items.Should().ContainSingle().Which.Name.Should().Be("Blue door");

        var detail = await admin.GetFromJsonAsync<AdminHouseholdDetailDto>($"/api/Households/admin/{household}");
        detail!.OwnerUserId.Should().Be(owner);
        detail.Members.Should().HaveCount(2);
        detail.Members.Single(m => m.UserId == member).Role.Should().Be("member");
        detail.Members.Single(m => m.UserId == owner).IsOwner.Should().BeTrue();
        detail.MealPlanCount.Should().Be(1);
        detail.ShoppingListCount.Should().Be(0);
    }

    [Fact]
    public async Task ASupportNoteIsVisibleToAnotherAdmin_AndNeverToANonAdmin()
    {
        await fixture.ResetDatabaseAsync();
        using var first = await AdminAsync("first");
        using var second = await AdminAsync("second");
        var (household, owner, _) = await SeedHouseholdAsync("Noted house", "noted@example.test", $"member-{Guid.NewGuid():N}@example.test");
        using var ownerClient = fixture.CreateAuthenticatedClient(owner, "noted@example.test");

        var created = await (await first.PostAsJsonAsync("/api/admin/support-notes", new CreateSupportNoteRequest("Household", household, "Asked for a refund on 3 Oct.")))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<SupportNoteDto>();
        created!.AuthorEmail.Should().Contain("first");

        var seen = await second.GetFromJsonAsync<List<SupportNoteDto>>($"/api/admin/support-notes?subjectType=Household&subjectId={household}");
        seen.Should().ContainSingle().Which.Body.Should().Be("Asked for a refund on 3 Oct.");

        var edited = await (await second.PutAsJsonAsync($"/api/admin/support-notes/{created.Id}", new UpdateSupportNoteRequest("Refund sent.")))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<SupportNoteDto>();
        edited!.Body.Should().Be("Refund sent.");
        edited.AuthorUserId.Should().Be(created.AuthorUserId, "editing does not change the author");

        (await ownerClient.GetAsync($"/api/admin/support-notes?subjectType=Household&subjectId={household}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ownerClient.PostAsJsonAsync("/api/admin/support-notes", new CreateSupportNoteRequest("User", owner, "Mine")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await (await ownerClient.GetAsync($"/api/Households/{household}")).Content.ReadAsStringAsync())
            .Should().NotContain("Refund sent.");

        (await second.DeleteAsync($"/api/admin/support-notes/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await first.GetFromJsonAsync<List<SupportNoteDto>>($"/api/admin/support-notes?subjectType=Household&subjectId={household}"))
            .Should().BeEmpty();
        (await first.PostAsJsonAsync("/api/admin/support-notes", new CreateSupportNoteRequest("User", Guid.NewGuid(), "Nobody")))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<HttpClient> AdminAsync(string label)
    {
        var id = Guid.NewGuid();
        var email = $"{label}-admin-{id:N}@example.test";
        await fixture.SeedUserAsync(id, email, role: "admin");
        return fixture.CreateAuthenticatedClient(id, email, "admin");
    }

    private async Task<(Guid Household, Guid Owner, Guid Member)> SeedHouseholdAsync(string name, string ownerEmail, string memberEmail)
    {
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        await fixture.SeedUserAsync(owner, ownerEmail);
        await fixture.SeedUserAsync(member, memberEmail);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        var household = new Household { Id = Guid.NewGuid(), Name = name, CreatedBy = owner, CreatedAt = DateTime.UtcNow };
        db.Households.Add(household);
        db.HouseholdMembers.Add(new HouseholdMember { HouseholdId = household.Id, UserId = owner, Role = "owner", JoinedAt = DateTime.UtcNow.AddDays(-3) });
        db.HouseholdMembers.Add(new HouseholdMember { HouseholdId = household.Id, UserId = member, Role = "member", JoinedAt = DateTime.UtcNow });
        db.MealPlans.Add(new MealPlan
        {
            Id = Guid.NewGuid(),
            UserId = owner,
            HouseholdId = household.Id,
            Name = $"{name} week",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return (household.Id, owner, member);
    }
}
