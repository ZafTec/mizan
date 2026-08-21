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



}
