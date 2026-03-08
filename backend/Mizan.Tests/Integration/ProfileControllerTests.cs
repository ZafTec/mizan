using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

[Collection("ApiIntegration")]
public class ProfileControllerTests
{
    private readonly ApiTestFixture _fixture;

    public ProfileControllerTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExportReturnsOnlyCurrentUsersData()
    {
        await _fixture.ResetDatabaseAsync();

        var userId = Guid.NewGuid();
        var userEmail = $"export-{userId:N}@example.com";
        var otherUserId = Guid.NewGuid();
        var otherUserEmail = $"export-{otherUserId:N}@example.com";

        await _fixture.SeedUserAsync(userId, userEmail);
        await _fixture.SeedUserAsync(otherUserId, otherUserEmail);

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();

            var user = await db.Users.FindAsync(userId);
            user.Should().NotBeNull();
            user!.ThemePreference = "dark";
            user.CompactMode = true;

            var userGoal = new UserGoal
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GoalType = "maintenance",
                TargetCalories = 2200,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            var otherGoal = new UserGoal
            {
                Id = Guid.NewGuid(),
                UserId = otherUserId,
                GoalType = "weight_loss",
                TargetCalories = 1500,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            db.UserGoals.AddRange(userGoal, otherGoal);

            db.FoodDiaryEntries.AddRange(
                new FoodDiaryEntry
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Name = "Chicken bowl",
                    MealType = "lunch",
                    EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    Servings = 1,
                    Calories = 650,
                    LoggedAt = DateTime.UtcNow,
                },
                new FoodDiaryEntry
                {
                    Id = Guid.NewGuid(),
                    UserId = otherUserId,
                    Name = "Leaked meal",
                    MealType = "dinner",
                    EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    Servings = 1,
                    Calories = 999,
                    LoggedAt = DateTime.UtcNow,
                }
            );

            await db.SaveChangesAsync();
        }

        using var client = _fixture.CreateAuthenticatedClient(userId, userEmail);
        var response = await client.GetAsync("/api/Profile/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        response.Content.Headers.ContentDisposition.Should().NotBeNull();
        response.Content.Headers.ContentDisposition!.FileName.Should().Contain("mizan-profile-export-");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        root.GetProperty("account").GetProperty("email").GetString().Should().Be(userEmail);
        root.GetProperty("account").GetProperty("appearance").GetProperty("theme").GetString().Should().Be("dark");
        root.GetProperty("account").GetProperty("appearance").GetProperty("compactMode").GetBoolean().Should().BeTrue();
        root.GetProperty("observations").GetProperty("totalMealsLogged").GetInt32().Should().Be(1);
        root.GetProperty("data").GetProperty("goals").GetArrayLength().Should().Be(1);
        root.GetProperty("data").GetProperty("meals").GetArrayLength().Should().Be(1);
        root.GetProperty("data").GetProperty("meals")[0].GetProperty("name").GetString().Should().Be("Chicken bowl");
    }
}
