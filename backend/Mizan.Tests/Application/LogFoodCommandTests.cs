using FluentAssertions;
using Mizan.Application.Commands;
using Xunit;

namespace Mizan.Tests.Application;

public class LogFoodCommandTests
{
    [Fact]
    public void Validator_ShouldFail_WhenNoFoodOrRecipeProvided()
    {
        var validator = new LogFoodCommandValidator();
        var command = new LogFoodCommand
        {
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MealType = "lunch",
            Servings = 1
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("FoodId or RecipeId"));
    }

    [Fact]
    public void Validator_ShouldFail_WhenInvalidMealType()
    {
        var validator = new LogFoodCommandValidator();
        var command = new LogFoodCommand
        {
            FoodId = Guid.NewGuid(),
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MealType = "brunch",
            Servings = 1
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("breakfast, lunch, dinner, or snack"));
    }

    [Fact]
    public void Validator_ShouldFail_WhenServingsNotPositive()
    {
        var validator = new LogFoodCommandValidator();
        var command = new LogFoodCommand
        {
            FoodId = Guid.NewGuid(),
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MealType = "lunch",
            Servings = 0
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("greater than 0"));
    }
}
