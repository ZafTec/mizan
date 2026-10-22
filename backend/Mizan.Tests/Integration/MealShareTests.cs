using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// A meal eaten with other people is sent whole with the head count, and the API logs one share. The rule lives
/// here once, so the bot, the website and an MCP client all log the same third of the same stew.
/// </summary>
[Collection("ApiIntegration")]
public class MealShareTests
{
    private readonly ApiTestFixture _fixture;

    public MealShareTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AMealSharedByThree_IsStoredAsOneThird_AndSaysSo()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync();

        var response = await client.PostAsJsonAsync("/api/Meals", new
        {
            name = "Stew",
            mealType = "DINNER",
            servings = 1,
            amountGrams = 900,
            calories = 1000,
            proteinGrams = 31,
            carbsGrams = 61,
            fatGrams = 11,
            fiberGrams = 5,
            sharedBetween = 3,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var entry = (await _fixture.GetFoodDiaryEntriesByUserId(userId)).Should().ContainSingle().Subject;
        entry.Name.Should().Be("Stew (1/3 share)");
        entry.Calories.Should().Be(333m);
        entry.ProteinGrams.Should().Be(10.3m);
        entry.CarbsGrams.Should().Be(20.3m);
        entry.FatGrams.Should().Be(3.7m);
        entry.FiberGrams.Should().Be(1.7m);
        entry.AmountGrams.Should().Be(300m);
    }

    [Fact]
    public async Task ANameThatAlreadySaysShare_IsNotLabelledTwice()
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync();

        (await client.PostAsJsonAsync("/api/Meals", new
        {
            name = "Stew (1/2 share)", mealType = "LUNCH", servings = 1, calories = 500, sharedBetween = 2,
        })).EnsureSuccessStatusCode();

        var entry = (await _fixture.GetFoodDiaryEntriesByUserId(userId)).Should().ContainSingle().Subject;
        entry.Name.Should().Be("Stew (1/2 share)");
        entry.Calories.Should().Be(250m);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(11)]
    public async Task AHeadCountOutsideTwoToTen_IsRefused_AndNothingIsLogged(int people)
    {
        await _fixture.ResetDatabaseAsync();
        var (client, userId) = await UserAsync();

        var response = await client.PostAsJsonAsync("/api/Meals", new
        {
            name = "Stew", mealType = "LUNCH", servings = 1, calories = 500, sharedBetween = people,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _fixture.GetFoodDiaryEntriesByUserId(userId)).Should().BeEmpty();
    }

    private async Task<(HttpClient Client, Guid UserId)> UserAsync()
    {
        var id = Guid.NewGuid();
        var email = $"share-{id:N}@example.com";
        await _fixture.SeedUserAsync(id, email);
        return (_fixture.CreateAuthenticatedClient(id, email), id);
    }
}
