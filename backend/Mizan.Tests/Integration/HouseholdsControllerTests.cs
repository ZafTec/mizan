using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Mizan.Tests.Integration;

// These cover the invitation-based membership flow that replaced the direct
// AddHouseholdMember endpoint. The creator is owner/admin, invites a
// registered user by email, the invitee accepts and becomes a member.
[Collection("ApiIntegration")]
public class HouseholdsControllerTests
{
    private readonly ApiTestFixture _fixture;

    public HouseholdsControllerTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AdminCanInviteAndMemberCanAccept()
    {
        await _fixture.ResetDatabaseAsync();

        var ownerId = Guid.NewGuid();
        var ownerEmail = $"owner-{ownerId:N}@example.com";
        await _fixture.SeedUserAsync(ownerId, ownerEmail);

        var memberId = Guid.NewGuid();
        var memberEmail = $"member-{memberId:N}@example.com";
        await _fixture.SeedUserAsync(memberId, memberEmail);

        using var ownerClient = _fixture.CreateAuthenticatedClient(ownerId, ownerEmail);

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Households", new { Name = "Test Household" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var householdId = await createResponse.Content.ReadFromJsonAsync<Guid>();
        householdId.Should().NotBe(Guid.Empty);

        var inviteResponse = await ownerClient.PostAsJsonAsync(
            $"/api/Households/{householdId}/invitations",
            new { Email = memberEmail, Role = "member" });
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var inviteBody = await inviteResponse.Content.ReadFromJsonAsync<InviteResult>();
        inviteBody!.Success.Should().BeTrue();
        inviteBody.InvitationId.Should().NotBeNull();

        using var memberClient = _fixture.CreateAuthenticatedClient(memberId, memberEmail);
        var acceptResponse = await memberClient.PostAsJsonAsync(
            $"/api/Households/invitations/{inviteBody.InvitationId}/respond",
            new { Action = "accept" });
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await memberClient.GetAsync($"/api/Households/{householdId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var household = await getResponse.Content.ReadFromJsonAsync<HouseholdResponse>();
        household.Should().NotBeNull();
        household!.Members.Should().Contain(m => m.Email == memberEmail);
    }

    [Fact]
    public async Task NonAdminCannotInvite()
    {
        await _fixture.ResetDatabaseAsync();

        var ownerId = Guid.NewGuid();
        var ownerEmail = $"owner-{ownerId:N}@example.com";
        await _fixture.SeedUserAsync(ownerId, ownerEmail);

        var memberId = Guid.NewGuid();
        var memberEmail = $"member-{memberId:N}@example.com";
        await _fixture.SeedUserAsync(memberId, memberEmail);

        var outsiderId = Guid.NewGuid();
        var outsiderEmail = $"member-{outsiderId:N}@example.com";
        await _fixture.SeedUserAsync(outsiderId, outsiderEmail);

        using var ownerClient = _fixture.CreateAuthenticatedClient(ownerId, ownerEmail);
        var createResponse = await ownerClient.PostAsJsonAsync("/api/Households", new { Name = "Team" });
        var householdId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        // Owner invites member as a plain 'member' role (not admin).
        var firstInvite = await ownerClient.PostAsJsonAsync(
            $"/api/Households/{householdId}/invitations",
            new { Email = memberEmail, Role = "member" });
        firstInvite.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await firstInvite.Content.ReadFromJsonAsync<InviteResult>();

        using var memberClient = _fixture.CreateAuthenticatedClient(memberId, memberEmail);
        var accept = await memberClient.PostAsJsonAsync(
            $"/api/Households/invitations/{firstBody!.InvitationId}/respond",
            new { Action = "accept" });
        accept.StatusCode.Should().Be(HttpStatusCode.OK);

        // Non-admin member tries to invite the outsider, must be rejected.
        var forbiddenResponse = await memberClient.PostAsJsonAsync(
            $"/api/Households/{householdId}/invitations",
            new { Email = outsiderEmail, Role = "member" });
        forbiddenResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CannotInviteUnregisteredEmail()
    {
        await _fixture.ResetDatabaseAsync();

        var ownerId = Guid.NewGuid();
        var ownerEmail = $"owner-{ownerId:N}@example.com";
        await _fixture.SeedUserAsync(ownerId, ownerEmail);

        using var ownerClient = _fixture.CreateAuthenticatedClient(ownerId, ownerEmail);
        var createResponse = await ownerClient.PostAsJsonAsync("/api/Households", new { Name = "Ghosts" });
        var householdId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var response = await ownerClient.PostAsJsonAsync(
            $"/api/Households/{householdId}/invitations",
            new { Email = "nobody@example.com", Role = "member" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record InviteResult(bool Success, Guid? InvitationId, string? Message);
    private sealed record HouseholdResponse(Guid Id, string Name, List<HouseholdMemberResponse> Members);
    private sealed record HouseholdMemberResponse(Guid UserId, string? Name, string? Email, string? Role, DateTime JoinedAt);
}
