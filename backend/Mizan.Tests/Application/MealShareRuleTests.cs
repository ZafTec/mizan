using FluentAssertions;
using Mizan.Domain.Nutrition;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>One person's share of a shared meal: whole calories, tenths of a gram, and a label that says so once.</summary>
public class MealShareRuleTests
{
    [Theory]
    [InlineData(1000, 3, 333)]
    [InlineData(600, 3, 200)]
    [InlineData(601, 2, 300)] // 300.5 rounds to even, as the rest of the log does
    [InlineData(0, 4, 0)]
    public void Calories_AreWholeNumbers(int whole, int people, int expected)
    {
        MealShare.OfCalories(whole, people).Should().Be(expected);
    }

    [Theory]
    [InlineData("31", 3, "10.3")]
    [InlineData("61", 3, "20.3")]
    [InlineData("11", 3, "3.7")]
    [InlineData("20", 3, "6.7")]
    [InlineData("300", 4, "75.0")]
    public void Grams_KeepOneDecimal(string whole, int people, string expected)
    {
        MealShare.OfGrams(decimal.Parse(whole), people).Should().Be(decimal.Parse(expected));
    }

    [Fact]
    public void AMissingValue_StaysMissing()
    {
        MealShare.OfCalories(null, 3).Should().BeNull();
        MealShare.OfGrams(null, 3).Should().BeNull();
    }

    [Theory]
    [InlineData("Pasta", 3, "Pasta (1/3 share)")]
    [InlineData("Pasta (1/3 share)", 3, "Pasta (1/3 share)")]
    [InlineData("Pasta (1/2 share)", 3, "Pasta (1/2 share) (1/3 share)")]
    [InlineData("  Stew  ", 2, "Stew (1/2 share)")]
    [InlineData("", 4, "(1/4 share)")]
    public void TheName_SaysItIsAShare_Once(string name, int people, string expected)
    {
        MealShare.Name(name, people).Should().Be(expected);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void TwoToTenPeople_IsAShare(int people, bool valid)
    {
        MealShare.IsValidPeople(people).Should().Be(valid);
    }
}
