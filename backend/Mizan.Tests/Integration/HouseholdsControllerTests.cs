using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public class HouseholdsControllerTests
{
    private readonly ApiTestFixture _fixture;

    public HouseholdsControllerTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AdminCanAddMembers_AndMembersCanViewHousehold()
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

        var addResponse = await ownerClient.PostAsJsonAsync($"/api/Households/{householdId}/members", new { Email = memberEmail });
        addResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var memberClient = _fixture.CreateAuthenticatedClient(memberId, memberEmail);
        var getResponse = await memberClient.GetAsync($"/api/Households/{householdId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var household = await getResponse.Content.ReadFromJsonAsync<HouseholdResponse>();
        household.Should().NotBeNull();
        household!.Members.Should().Contain(m => m.Email == memberEmail);
    }

    [Fact]
    public async Task NonAdminCannotAddMembers()
    {
        await _fixture.ResetDatabaseAsync();

        var ownerId = Guid.NewGuid();
        var ownerEmail = $"owner-{ownerId:N}@example.com";
        await _fixture.SeedUserAsync(ownerId, ownerEmail);

        var memberId = Guid.NewGuid();
        var memberEmail = $"member-{memberId:N}@example.com";
        await _fixture.SeedUserAsync(memberId, memberEmail);

        var newMemberId = Guid.NewGuid();
        var newMemberEmail = $"member-{newMemberId:N}@example.com";
        await _fixture.SeedUserAsync(newMemberId, newMemberEmail);

        using var ownerClient = _fixture.CreateAuthenticatedClient(ownerId, ownerEmail);
        var createResponse = await ownerClient.PostAsJsonAsync("/api/Households", new { Name = "Team" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var householdId = await createResponse.Content.ReadFromJsonAsync<Guid>();
        householdId.Should().NotBe(Guid.Empty);

        var addResponse = await ownerClient.PostAsJsonAsync($"/api/Households/{householdId}/members", new { Email = memberEmail });
        addResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var memberClient = _fixture.CreateAuthenticatedClient(memberId, memberEmail);
        var forbiddenResponse = await memberClient.PostAsJsonAsync($"/api/Households/{householdId}/members", new { Email = newMemberEmail });
        forbiddenResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record HouseholdResponse(Guid Id, string Name, List<HouseholdMemberResponse> Members);
    private sealed record HouseholdMemberResponse(Guid UserId, string? Name, string? Email, string? Role, DateTime JoinedAt);
}
