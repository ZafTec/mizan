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

    [Fact]
    public async Task Handle_ShouldCreateRecipe_WithSubRecipeIngredient()
    {
        // Arrange
        var subRecipe = new Domain.Entities.Recipe
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Title = "Tomato Sauce",
            Servings = 4,
            IsPublic = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Nutrition = new Domain.Entities.RecipeNutrition
            {
                RecipeId = Guid.NewGuid(),
                CaloriesPerServing = 50,
                ProteinGrams = 2,
                CarbsGrams = 8,
                FatGrams = 1
            }
        };
        _context.Recipes.Add(subRecipe);
        await _context.SaveChangesAsync();

        var command = new CreateRecipeCommand
        {
            Title = "Pasta with Sauce",
            Servings = 2,
            Ingredients = new List<CreateRecipeIngredientDto>
            {
                new() { IngredientText = "Pasta", Amount = 200, Unit = "g" },
                new() { SubRecipeId = subRecipe.Id, IngredientText = "Tomato Sauce", Amount = 2, Unit = "servings" }
            },
            Instructions = new List<string> { "Cook pasta", "Add sauce" },
            Tags = new List<string> { "italian" }
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .Include(r => r.Nutrition)
            .FirstAsync(r => r.Id == result.Id);

        recipe.Ingredients.Should().HaveCount(2);
        recipe.Ingredients.Should().Contain(i => i.SubRecipeId == subRecipe.Id);

        // Nutrition should include sub-recipe nutrition (2 servings * 50 cal = 100 cal total, divided by 2 servings = 50 cal per serving)
        recipe.Nutrition.Should().NotBeNull();
        recipe.Nutrition!.CaloriesPerServing.Should().Be(50); // 100 / 2 servings
        recipe.Nutrition.ProteinGrams.Should().Be(2); // 4 / 2 servings
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCircularDependencyDetected()
    {
        // Arrange: Create Recipe A that uses Recipe B
        var recipeB = new Domain.Entities.Recipe
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Title = "Recipe B",
            Servings = 2,
            IsPublic = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Recipes.Add(recipeB);
        await _context.SaveChangesAsync();

        // Create Recipe A that uses Recipe B
        var recipeA = new Domain.Entities.Recipe
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Title = "Recipe A",
            Servings = 2,
            IsPublic = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        recipeA.Ingredients.Add(new Domain.Entities.RecipeIngredient
        {
            Id = Guid.NewGuid(),
            RecipeId = recipeA.Id,
            SubRecipeId = recipeB.Id,
            IngredientText = "Recipe B",
            Amount = 1,
            Unit = "serving"
        });
        _context.Recipes.Add(recipeA);
        await _context.SaveChangesAsync();

        // Try to create a new version of Recipe B that uses Recipe A (would create cycle)
        var command = new CreateRecipeCommand
        {
            Title = "Recipe B Updated",
            Servings = 2,
            Ingredients = new List<CreateRecipeIngredientDto>
            {
                new() { SubRecipeId = recipeA.Id, IngredientText = "Recipe A", Amount = 1, Unit = "serving" }
            },
            Instructions = new List<string> { "Use Recipe A" },
            Tags = new List<string>()
        };

        // Act & Assert
        var act = async () => await _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<FluentValidation.ValidationException>()
            .WithMessage("*circular dependency*");
    }

    [Fact]
    public void Validator_ShouldFail_WhenBothFoodIdAndSubRecipeIdProvided()
    {
        // Arrange
        var validator = new CreateRecipeCommandValidator();
        var command = new CreateRecipeCommand
        {
            Title = "Test Recipe",
            Ingredients = new List<CreateRecipeIngredientDto>
            {
                new()
                {
                    FoodId = Guid.NewGuid(),
                    SubRecipeId = Guid.NewGuid(),
                    IngredientText = "Test ingredient",
                    Amount = 100,
                    Unit = "g"
                }
            }
        };

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("either FoodId or SubRecipeId"));
    }

    [Fact]
    public void Validator_ShouldFail_WhenSubRecipeIdUsedWithInvalidUnit()
    {
        // Arrange
        var validator = new CreateRecipeCommandValidator();
        var command = new CreateRecipeCommand
        {
            Title = "Test Recipe",
            Ingredients = new List<CreateRecipeIngredientDto>
            {
                new()
                {
                    SubRecipeId = Guid.NewGuid(),
                    IngredientText = "Test sub-recipe",
                    Amount = 2,
                    Unit = "grams" // Should be "serving" or "servings"
                }
            }
        };

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("serving"));
    }

    [Fact]
    public void Validator_ShouldPass_WhenSubRecipeIdUsedWithServingUnit()
    {
        // Arrange
        var validator = new CreateRecipeCommandValidator();
        var command = new CreateRecipeCommand
        {
            Title = "Test Recipe",
            Servings = 2,
            Ingredients = new List<CreateRecipeIngredientDto>
            {
                new()
                {
                    SubRecipeId = Guid.NewGuid(),
                    IngredientText = "Test sub-recipe",
                    Amount = 2,
                    Unit = "servings"
                }
            },
            Instructions = new List<string> { "Test" },
            Tags = new List<string>()
        };

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
