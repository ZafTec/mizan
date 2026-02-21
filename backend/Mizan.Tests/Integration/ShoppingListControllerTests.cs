using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public class ShoppingListControllerTests
{
    private readonly ApiTestFixture _fixture;

    public ShoppingListControllerTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UserCanCreateAndListShoppingLists()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"shopper-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var createResponse = await client.PostAsJsonAsync("/api/ShoppingLists", new { Name = "Weekly Groceries" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listId = await createResponse.Content.ReadFromJsonAsync<Guid>();
        listId.Should().NotBe(Guid.Empty);

        var listResponse = await client.GetAsync("/api/ShoppingLists");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var lists = await listResponse.Content.ReadFromJsonAsync<ShoppingListPagedResult>();
        lists.Should().NotBeNull();
        lists!.TotalCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task UserCanAddItemsToShoppingList()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"shopper-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var createResponse = await client.PostAsJsonAsync("/api/ShoppingLists", new { Name = "Test List" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var listId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var addItemResponse = await client.PostAsJsonAsync($"/api/ShoppingLists/{listId}/items", new
        {
            ItemName = "Chicken Breast",
            Amount = 500m,
            Unit = "g",
            Category = "Meat"
        });
        addItemResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var getResponse = await client.GetAsync($"/api/ShoppingLists/{listId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await getResponse.Content.ReadFromJsonAsync<ShoppingListDetailResult>();
        list.Should().NotBeNull();
        list!.Items.Should().Contain(i => i.ItemName == "Chicken Breast");
    }

    [Fact]
    public async Task UserCannotSeeOtherUsersShoppingLists()
    {
        await _fixture.ResetDatabaseAsync();

        var userId1 = Guid.NewGuid();
        var email1 = $"user1-{userId1:N}@example.com";
        await _fixture.SeedUserAsync(userId1, email1);

        var userId2 = Guid.NewGuid();
        var email2 = $"user2-{userId2:N}@example.com";
        await _fixture.SeedUserAsync(userId2, email2);

        using var client1 = _fixture.CreateAuthenticatedClient(userId1, email1);
        var createResponse = await client1.PostAsJsonAsync("/api/ShoppingLists", new { Name = "Private List" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var listId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        using var client2 = _fixture.CreateAuthenticatedClient(userId2, email2);
        var getResponse = await client2.GetAsync($"/api/ShoppingLists/{listId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UserCanDeleteOwnShoppingList()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var email = $"shopper-{userId:N}@example.com";
        await _fixture.SeedUserAsync(userId, email);

        using var client = _fixture.CreateAuthenticatedClient(userId, email);

        var createResponse = await client.PostAsJsonAsync("/api/ShoppingLists", new { Name = "To Delete" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var listId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var deleteResponse = await client.DeleteAsync($"/api/ShoppingLists/{listId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await client.GetAsync($"/api/ShoppingLists/{listId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record ShoppingListPagedResult(int TotalCount, List<object> Items);
    private sealed record ShoppingListDetailResult(Guid Id, string Name, List<ShoppingListItemResult> Items);
    private sealed record ShoppingListItemResult(Guid Id, string ItemName, decimal? Amount, string? Unit, string? Category, bool IsChecked);
}
