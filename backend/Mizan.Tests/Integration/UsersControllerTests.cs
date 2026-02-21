using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public class UsersControllerTests
{
    private readonly ApiTestFixture _fixture;

    public UsersControllerTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetMe_ReturnsAuthenticatedUserProfile()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"user-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var response = await client.GetAsync("/api/Users/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await response.Content.ReadFromJsonAsync<UserProfileResult>();
        user.Should().NotBeNull();
        user!.Email.Should().Be(email);
    }

    [Fact]
    public async Task GetMe_Returns401_WhenUnauthenticated()
    {
        await _fixture.ResetDatabaseAsync();

        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/Users/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateMe_UpdatesUserProfile()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"user-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var updateCommand = new { Name = "Updated Name", Image = (string?)null };
        var updateResponse = await client.PutAsJsonAsync("/api/Users/me", updateCommand);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync("/api/Users/me");
        var user = await getResponse.Content.ReadFromJsonAsync<UserProfileResult>();
        user!.Name.Should().Be("Updated Name");
    }

    private sealed record UserProfileResult(Guid Id, string Email, string? Name, string? Role);
}
