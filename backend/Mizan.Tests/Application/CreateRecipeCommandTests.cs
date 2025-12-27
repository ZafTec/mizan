using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mizan.Application.Commands;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.Data;
using Moq;
using Xunit;

namespace Mizan.Tests.Application;

public class CreateRecipeCommandTests : IDisposable
{
    private readonly MizanDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<ILogger<CreateRecipeCommandHandler>> _loggerMock;
    private readonly CreateRecipeCommandHandler _handler;
    private readonly Guid _testUserId = Guid.NewGuid();

    public CreateRecipeCommandTests()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MizanDbContext(options);

        _currentUserMock = new Mock<ICurrentUserService>();
        _currentUserMock.Setup(x => x.UserId).Returns(_testUserId);
        _currentUserMock.Setup(x => x.IsAuthenticated).Returns(true);

        _loggerMock = new Mock<ILogger<CreateRecipeCommandHandler>>();

        _handler = new CreateRecipeCommandHandler(_context, _currentUserMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldCreateRecipe_WithAllDetails()
    {
        // Arrange
        var command = new CreateRecipeCommand
        {
            Title = "Ethiopian Doro Wat",
            Description = "Traditional Ethiopian chicken stew",
            Servings = 4,
            PrepTimeMinutes = 30,
            CookTimeMinutes = 120,
            IsPublic = true,
            Ingredients = new List<CreateRecipeIngredientDto>
            {
                new() { IngredientText = "2 lbs chicken thighs", Amount = 900, Unit = "g" },
                new() { IngredientText = "4 large onions, finely chopped", Amount = 400, Unit = "g" },
                new() { IngredientText = "4 tbsp berbere spice", Amount = 60, Unit = "g" }
            },
            Instructions = new List<string>
            {
                "Dry sauté onions until caramelized (about 45 minutes)",
                "Add berbere spice and cook for 5 minutes",
                "Add chicken and cook until done",
                "Serve with injera"
            },
            Tags = new List<string> { "ethiopian", "chicken", "spicy" },
            Nutrition = new CreateRecipeNutritionDto
            {
                CaloriesPerServing = 350,
                ProteinGrams = 28,
                CarbsGrams = 15,
                FatGrams = 18
            }
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Title.Should().Be("Ethiopian Doro Wat");

        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .Include(r => r.Instructions)
            .Include(r => r.Tags)
            .Include(r => r.Nutrition)
            .FirstAsync(r => r.Id == result.Id);

        recipe.UserId.Should().Be(_testUserId);
        recipe.Ingredients.Should().HaveCount(3);
        recipe.Instructions.Should().HaveCount(4);
        recipe.Tags.Should().HaveCount(3);
        recipe.Nutrition.Should().NotBeNull();
        recipe.Nutrition!.CaloriesPerServing.Should().Be(350);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserNotAuthenticated()
    {
        // Arrange
        _currentUserMock.Setup(x => x.UserId).Returns((Guid?)null);

        var command = new CreateRecipeCommand
        {
            Title = "Test Recipe",
            Ingredients = new List<CreateRecipeIngredientDto>
            {
                new() { IngredientText = "Test ingredient" }
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public void Validator_ShouldFail_WhenTitleEmpty()
    {
        // Arrange
        var validator = new CreateRecipeCommandValidator();
        var command = new CreateRecipeCommand
        {
            Title = "",
            Ingredients = new List<CreateRecipeIngredientDto>
            {
                new() { IngredientText = "Test ingredient" }
            }
        };

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title");
    }

    [Fact]
    public void Validator_ShouldFail_WhenNoIngredients()
    {
        // Arrange
        var validator = new CreateRecipeCommandValidator();
        var command = new CreateRecipeCommand
        {
            Title = "Test Recipe",
            Ingredients = new List<CreateRecipeIngredientDto>()
        };

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("ingredient"));
    }

    [Fact]
    public void Validator_ShouldFail_WhenServingsNotPositive()
    {
        // Arrange
        var validator = new CreateRecipeCommandValidator();
        var command = new CreateRecipeCommand
        {
            Title = "Test Recipe",
            Servings = 0,
            Ingredients = new List<CreateRecipeIngredientDto>
            {
                new() { IngredientText = "Test" }
            }
        };

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
