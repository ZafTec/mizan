using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
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
    public async Task NonAdminCreatesAPrivateFood()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"user-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email, role: "user");

        using var client = _fixture.CreateAuthenticatedClient(userId, email, role: "user");

        var createCommand = new
        {
            Name = "My Private Food",
            CaloriesPer100g = 80m,
            ProteinPer100g = 8m,
            CarbsPer100g = 12m,
            FatPer100g = 3m
        };

        var response = await client.PostAsJsonAsync("/api/Foods", createCommand);

        // Creation was admin-only until Food gained an owner - see
        // docs/ARCHITECTURE.md#navigation-and-logging. A non-admin now gets a food private to them.
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var mine = await _fixture.GetFoodsByUserId(userId);
        mine.Should().ContainSingle(f => f.Name == "My Private Food");

        var publicFoods = await _fixture.GetPublicFoodsAsync();
        publicFoods.Should().NotContain(f => f.Name == "My Private Food");

        var foodId = mine.Single(f => f.Name == "My Private Food").Id;
        using var anonymous = _fixture.CreateClient();
        (await anonymous.GetAsync($"/api/Foods/{foodId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var otherId = Guid.NewGuid();
        await _fixture.SeedUserAsync(otherId, $"other-{otherId}@example.com");
        using var other = _fixture.CreateAuthenticatedClient(otherId, $"other-{otherId}@example.com");
        (await other.GetAsync($"/api/Foods/{foodId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var service = _fixture.CreateClient();
        service.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
        service.DefaultRequestHeaders.Add("X-Impersonate-User", userId.ToString());
        (await service.GetAsync($"/api/Foods/{foodId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var search = await service.GetFromJsonAsync<SearchFoodsResponse>("/api/Foods/search?searchTerm=My%20Private%20Food");
        search!.Items.Should().Contain(f => f.Id == foodId);
    }

    [Fact]
    public async Task APersonFindsAFoodByAnyPartOfItsNameOrBrand_IgnoringCase()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"user-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
            foreach (var (name, brand) in new[] { ("Greek Yogurt", "Fage"), ("Plain yogurt 2%", (string?)null), ("Oat milk", "Oatly"), ("Rolled oats", null) })
                db.Foods.Add(new Food { Id = Guid.NewGuid(), Name = name, Brand = brand, CaloriesPer100g = 100m, ProteinPer100g = 5m, CarbsPer100g = 10m, FatPer100g = 2m });
            await db.SaveChangesAsync();
        }

        using var client = _fixture.CreateAuthenticatedClient(userId, email);
        async Task<IEnumerable<string>> NamesAsync(string term) =>
            (await client.GetFromJsonAsync<SearchFoodsResponse>($"/api/Foods/search?searchTerm={Uri.EscapeDataString(term)}"))!.Items.Select(f => f.Name);

        (await NamesAsync("YOGURT")).Should().Equal("Greek Yogurt", "Plain yogurt 2%");
        (await NamesAsync("oatl")).Should().Equal("Oat milk");
        (await NamesAsync("oat")).Should().Equal("Oat milk", "Rolled oats");
        (await NamesAsync("2%")).Should().Equal("Plain yogurt 2%");
        (await NamesAsync("t_m")).Should().BeEmpty();
    }

    private sealed record CreateFoodResponse(Guid Id, bool Success);
    private sealed record FoodResponse(Guid Id, string Name);
    private sealed record SearchFoodsResponse(List<FoodResponse> Items, int TotalCount);
}
