using FluentAssertions;
using Mizan.Application.Commands;
using Xunit;

namespace Mizan.Tests.Application;

public class CreateRecipeCommandTests
{
    [Fact]
    public void Validator_ShouldFail_WhenTitleEmpty()
    {
        var validator = new CreateRecipeCommandValidator();
        var command = new CreateRecipeCommand
        {
            Title = "",
            Ingredients = new List<CreateRecipeIngredientDto>
            {
                new() { IngredientText = "Test ingredient" }
            }
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title");
    }

    [Fact]
    public void Validator_ShouldFail_WhenNoIngredients()
    {
        var validator = new CreateRecipeCommandValidator();
        var command = new CreateRecipeCommand
        {
            Title = "Test Recipe",
            Ingredients = new List<CreateRecipeIngredientDto>()
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("ingredient"));
    }

    [Fact]
    public void Validator_ShouldFail_WhenServingsNotPositive()
    {
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

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_ShouldFail_WhenBothFoodIdAndSubRecipeIdProvided()
    {
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

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("either FoodId or SubRecipeId"));
    }

    [Fact]
    public void Validator_ShouldFail_WhenSubRecipeIdUsedWithInvalidUnit()
    {
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
                    Unit = "grams"
                }
            }
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("serving"));
    }

    [Fact]
    public void Validator_ShouldPass_WhenSubRecipeIdUsedWithServingUnit()
    {
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

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
