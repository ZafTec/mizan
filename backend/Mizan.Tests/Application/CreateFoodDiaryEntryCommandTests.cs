using FluentAssertions;
using Mizan.Application.Commands;
using Xunit;

namespace Mizan.Tests.Application;

public class CreateFoodDiaryEntryCommandTests
{
    [Fact]
    public void Validator_ShouldFail_WhenNoFoodRecipeOrName()
    {
        var validator = new CreateFoodDiaryEntryCommandValidator();
        var command = new CreateFoodDiaryEntryCommand
        {
            MealType = "MEAL",
            Servings = 1
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("foodId") || e.ErrorMessage.Contains("recipeId") || e.ErrorMessage.Contains("name"));
    }

    [Fact]
    public void Validator_ShouldPass_WhenManualEntryWithName()
    {
        var validator = new CreateFoodDiaryEntryCommandValidator();
        var command = new CreateFoodDiaryEntryCommand
        {
            Name = "Homemade Salad",
            MealType = "MEAL",
            Servings = 1,
            Calories = 250,
            ProteinGrams = 20,
            CarbsGrams = 15,
            FatGrams = 8,
            FiberGrams = 4
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ShouldFail_WhenFiberIsNegative()
    {
        var validator = new CreateFoodDiaryEntryCommandValidator();
        var command = new CreateFoodDiaryEntryCommand
        {
            Name = "Test Meal",
            MealType = "MEAL",
            Servings = 1,
            FiberGrams = -1m
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FiberGrams");
    }

    [Fact]
    public void Validator_ShouldPass_WhenFiberIsNull()
    {
        var validator = new CreateFoodDiaryEntryCommandValidator();
        var command = new CreateFoodDiaryEntryCommand
        {
            Name = "Test Meal",
            MealType = "MEAL",
            Servings = 1,
            FiberGrams = null
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ShouldPass_WhenFiberIsZero()
    {
        var validator = new CreateFoodDiaryEntryCommandValidator();
        var command = new CreateFoodDiaryEntryCommand
        {
            Name = "Test Meal",
            MealType = "MEAL",
            Servings = 1,
            FiberGrams = 0m
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("MEAL")]
    [InlineData("SNACK")]
    [InlineData("DRINK")]
    public void Validator_ShouldPass_ForAllValidMealTypes(string mealType)
    {
        var validator = new CreateFoodDiaryEntryCommandValidator();
        var command = new CreateFoodDiaryEntryCommand
        {
            Name = "Test",
            MealType = mealType,
            Servings = 1
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ShouldFail_WhenInvalidMealType()
    {
        var validator = new CreateFoodDiaryEntryCommandValidator();
        var command = new CreateFoodDiaryEntryCommand
        {
            Name = "Test",
            MealType = "BREAKFAST",
            Servings = 1
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_ShouldFail_WhenCaloriesNegative()
    {
        var validator = new CreateFoodDiaryEntryCommandValidator();
        var command = new CreateFoodDiaryEntryCommand
        {
            Name = "Test",
            MealType = "MEAL",
            Servings = 1,
            Calories = -100
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Calories");
    }

    [Fact]
    public void Validator_ShouldFail_WhenServingsIsZero()
    {
        var validator = new CreateFoodDiaryEntryCommandValidator();
        var command = new CreateFoodDiaryEntryCommand
        {
            Name = "Test",
            MealType = "MEAL",
            Servings = 0
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Servings");
    }
}
