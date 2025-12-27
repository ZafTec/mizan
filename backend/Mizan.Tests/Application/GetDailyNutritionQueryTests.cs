using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Application.Queries;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Moq;
using Xunit;

namespace Mizan.Tests.Application;

public class GetDailyNutritionQueryTests : IDisposable
{
    private readonly MizanDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly GetDailyNutritionQueryHandler _handler;
    private readonly Guid _testUserId = Guid.NewGuid();

    public GetDailyNutritionQueryTests()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MizanDbContext(options);

        _currentUserMock = new Mock<ICurrentUserService>();
        _currentUserMock.Setup(x => x.UserId).Returns(_testUserId);
        _currentUserMock.Setup(x => x.IsAuthenticated).Returns(true);

        _handler = new GetDailyNutritionQueryHandler(_context, _currentUserMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnDailySummary_WithMealBreakdown()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Add some food diary entries
        _context.FoodDiaryEntries.AddRange(
            new FoodDiaryEntry
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                EntryDate = today,
                MealType = "breakfast",
                Calories = 400,
                ProteinGrams = 20,
                CarbsGrams = 50,
                FatGrams = 15
            },
            new FoodDiaryEntry
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                EntryDate = today,
                MealType = "lunch",
                Calories = 600,
                ProteinGrams = 35,
                CarbsGrams = 60,
                FatGrams = 20
            },
            new FoodDiaryEntry
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                EntryDate = today,
                MealType = "lunch",
                Calories = 150,
                ProteinGrams = 5,
                CarbsGrams = 20,
                FatGrams = 5
            }
        );

        // Add user goal
        _context.UserGoals.Add(new UserGoal
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            TargetCalories = 2000,
            TargetProteinGrams = 150,
            TargetCarbsGrams = 200,
            TargetFatGrams = 65,
            IsActive = true
        });

        await _context.SaveChangesAsync();

        var query = new GetDailyNutritionQuery { Date = today };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Date.Should().Be(today);
        result.TotalCalories.Should().Be(1150);
        result.TotalProtein.Should().Be(60);
        result.TotalCarbs.Should().Be(130);
        result.TotalFat.Should().Be(40);

        result.TargetCalories.Should().Be(2000);
        result.TargetProtein.Should().Be(150);

        result.MealBreakdown.Should().HaveCount(2); // breakfast and lunch
        result.MealBreakdown.Should().Contain(m => m.MealType == "breakfast" && m.Calories == 400);
        result.MealBreakdown.Should().Contain(m => m.MealType == "lunch" && m.Calories == 750);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptySummary_WhenNoEntries()
    {
        // Arrange
        var query = new GetDailyNutritionQuery { Date = DateOnly.FromDateTime(DateTime.UtcNow) };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalCalories.Should().Be(0);
        result.MealBreakdown.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldOnlyReturnCurrentUserEntries()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var otherUserId = Guid.NewGuid();

        _context.FoodDiaryEntries.AddRange(
            new FoodDiaryEntry
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                EntryDate = today,
                MealType = "breakfast",
                Calories = 400
            },
            new FoodDiaryEntry
            {
                Id = Guid.NewGuid(),
                UserId = otherUserId, // Different user
                EntryDate = today,
                MealType = "breakfast",
                Calories = 500
            }
        );

        await _context.SaveChangesAsync();

        var query = new GetDailyNutritionQuery { Date = today };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.TotalCalories.Should().Be(400); // Only current user's entries
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserNotAuthenticated()
    {
        // Arrange
        _currentUserMock.Setup(x => x.UserId).Returns((Guid?)null);

        var query = new GetDailyNutritionQuery { Date = DateOnly.FromDateTime(DateTime.UtcNow) };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(query, CancellationToken.None));
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
