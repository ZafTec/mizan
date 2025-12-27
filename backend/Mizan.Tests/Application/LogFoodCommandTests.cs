using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Commands;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Moq;
using Xunit;

namespace Mizan.Tests.Application;

public class LogFoodCommandTests : IDisposable
{
    private readonly MizanDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly LogFoodCommandHandler _handler;
    private readonly Guid _testUserId = Guid.NewGuid();

    public LogFoodCommandTests()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MizanDbContext(options);

        _currentUserMock = new Mock<ICurrentUserService>();
        _currentUserMock.Setup(x => x.UserId).Returns(_testUserId);
        _currentUserMock.Setup(x => x.IsAuthenticated).Returns(true);

        _handler = new LogFoodCommandHandler(_context, _currentUserMock.Object);

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        var food = new Food
        {
            Id = Guid.NewGuid(),
            Name = "Chicken Breast",
            CaloriesPer100g = 165,
            ProteinPer100g = 31,
            CarbsPer100g = 0,
            FatPer100g = 3.6m,
            ServingSize = 100,
            ServingUnit = "g",
            IsVerified = true
        };

        _context.Foods.Add(food);
        _context.SaveChanges();
    }

    [Fact]
    public async Task Handle_ShouldLogFood_WhenFoodExists()
    {
        // Arrange
        var food = await _context.Foods.FirstAsync();
        var command = new LogFoodCommand
        {
            FoodId = food.Id,
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MealType = "lunch",
            Servings = 1.5m
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Calories.Should().Be(165 * 1.5m);
        result.ProteinGrams.Should().Be(31 * 1.5m);
        result.Message.Should().Contain("Chicken Breast");

        var entry = await _context.FoodDiaryEntries.FirstAsync();
        entry.UserId.Should().Be(_testUserId);
        entry.MealType.Should().Be("lunch");
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserNotAuthenticated()
    {
        // Arrange
        _currentUserMock.Setup(x => x.UserId).Returns((Guid?)null);

        var command = new LogFoodCommand
        {
            FoodId = Guid.NewGuid(),
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MealType = "breakfast",
            Servings = 1
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public void Validator_ShouldFail_WhenNoFoodOrRecipeProvided()
    {
        // Arrange
        var validator = new LogFoodCommandValidator();
        var command = new LogFoodCommand
        {
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MealType = "lunch",
            Servings = 1
        };

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("FoodId or RecipeId"));
    }

    [Fact]
    public void Validator_ShouldFail_WhenInvalidMealType()
    {
        // Arrange
        var validator = new LogFoodCommandValidator();
        var command = new LogFoodCommand
        {
            FoodId = Guid.NewGuid(),
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MealType = "brunch", // Invalid
            Servings = 1
        };

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("breakfast, lunch, dinner, or snack"));
    }

    [Fact]
    public void Validator_ShouldFail_WhenServingsNotPositive()
    {
        // Arrange
        var validator = new LogFoodCommandValidator();
        var command = new LogFoodCommand
        {
            FoodId = Guid.NewGuid(),
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MealType = "lunch",
            Servings = 0 // Invalid
        };

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("greater than 0"));
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
