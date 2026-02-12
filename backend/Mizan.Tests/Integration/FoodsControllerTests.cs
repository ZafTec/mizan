using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public class FoodsControllerTests
{
    private readonly ApiTestFixture _fixture;

    public FoodsControllerTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AdminCanCreateUpdateDeleteFood()
    {
        await _fixture.ResetDatabaseAsync();

        var adminId = Guid.NewGuid();
        var adminEmail = $"admin-{adminId:N}@example.com";
        await _fixture.SeedUserAsync(adminId, adminEmail, role: "admin");

        using var client = _fixture.CreateAuthenticatedClient(adminId, adminEmail, role: "admin");

        var createCommand = new
        {
            Name = "Test Food",
            CaloriesPer100g = 100m,
            ProteinPer100g = 10m,
            CarbsPer100g = 20m,
            FatPer100g = 5m,
            IsVerified = true
        };

        var createResponse = await client.PostAsJsonAsync("/api/Foods", createCommand);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateFoodResponse>();
        created.Should().NotBeNull();
        created!.Success.Should().BeTrue();

        var getResponse = await client.GetAsync($"/api/Foods/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await getResponse.Content.ReadFromJsonAsync<FoodResponse>();
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("Test Food");

        var updateCommand = new
        {
            Id = created.Id,
            Name = "Updated Food",
            CaloriesPer100g = 150m,
            ProteinPer100g = 15m,
            CarbsPer100g = 25m,
            FatPer100g = 7m,
            IsVerified = false
        };

        var updateResponse = await client.PutAsJsonAsync($"/api/Foods/{created.Id}", updateCommand);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var searchResponse = await client.GetAsync("/api/Foods/search?searchTerm=updated");
        searchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var searchResult = await searchResponse.Content.ReadFromJsonAsync<SearchFoodsResponse>();
        searchResult.Should().NotBeNull();
        searchResult!.Items.Should().Contain(f => f.Name == "Updated Food");

        var deleteResponse = await client.DeleteAsync($"/api/Foods/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getDeleted = await client.GetAsync($"/api/Foods/{created.Id}");
        getDeleted.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonAdminCannotCreateFood()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"user-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email, role: "user");

        using var client = _fixture.CreateAuthenticatedClient(userId, email, role: "user");

        var createCommand = new
        {
            Name = "Blocked Food",
            CaloriesPer100g = 80m,
            ProteinPer100g = 8m,
            CarbsPer100g = 12m,
            FatPer100g = 3m
        };

        var response = await client.PostAsJsonAsync("/api/Foods", createCommand);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record CreateFoodResponse(Guid Id, bool Success);
    private sealed record FoodResponse(Guid Id, string Name);
    private sealed record SearchFoodsResponse(List<FoodResponse> Items, int TotalCount);
}
