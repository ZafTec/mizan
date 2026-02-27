using FluentAssertions;
using Mizan.Application.Services;
using Xunit;

namespace Mizan.Tests.Application;

public class NutritionHintsTests
{
    [Fact]
    public void CheckConsistency_NoWarnings_WhenAllValuesConsistent()
    {
        // 20g protein (80) + 30g carbs (120) + 10g fat (90) = 290 kcal
        var hints = NutritionHints.CheckConsistency(
            calories: 290m,
            proteinGrams: 20m,
            carbsGrams: 30m,
            fatGrams: 10m,
            fiberGrams: null);

        hints.Should().BeEmpty();
    }

    [Fact]
    public void CheckConsistency_NoWarnings_WhenCaloriesWithinTolerance()
    {
        // Atwater = 290. 10% deviation (261) is within 20% threshold.
        var hints = NutritionHints.CheckConsistency(
            calories: 261m,
            proteinGrams: 20m,
            carbsGrams: 30m,
            fatGrams: 10m,
            fiberGrams: null);

        hints.Should().BeEmpty();
    }

    [Fact]
    public void CheckConsistency_Warning_WhenCaloriesExceedThreshold()
    {
        // Atwater = 290. Entered 600 — 107% deviation.
        var hints = NutritionHints.CheckConsistency(
            calories: 600m,
            proteinGrams: 20m,
            carbsGrams: 30m,
            fatGrams: 10m,
            fiberGrams: null);

        hints.Should().ContainSingle(h => h.Contains("macro-based estimate"));
    }

    [Fact]
    public void CheckConsistency_Warning_WhenCaloriesSignificantlyLow()
    {
        // Atwater = 290. Entered 50 — 83% deviation.
        var hints = NutritionHints.CheckConsistency(
            calories: 50m,
            proteinGrams: 20m,
            carbsGrams: 30m,
            fatGrams: 10m,
            fiberGrams: null);

        hints.Should().ContainSingle(h => h.Contains("macro-based estimate"));
    }

    [Fact]
    public void CheckConsistency_Warning_WhenFiberExceedsCarbs()
    {
        var hints = NutritionHints.CheckConsistency(
            calories: null,
            proteinGrams: null,
            carbsGrams: 10m,
            fatGrams: null,
            fiberGrams: 15m);

        hints.Should().ContainSingle(h => h.Contains("Fiber") && h.Contains("carbohydrate"));
    }

    [Fact]
    public void CheckConsistency_NoWarning_WhenFiberEqualsCarbs()
    {
        var hints = NutritionHints.CheckConsistency(
            calories: null,
            proteinGrams: null,
            carbsGrams: 10m,
            fatGrams: null,
            fiberGrams: 10m);

        hints.Should().BeEmpty();
    }

    [Fact]
    public void CheckConsistency_NoWarning_WhenFiberLessThanCarbs()
    {
        var hints = NutritionHints.CheckConsistency(
            calories: null,
            proteinGrams: null,
            carbsGrams: 30m,
            fatGrams: null,
            fiberGrams: 8m);

        hints.Should().BeEmpty();
    }

    [Fact]
    public void CheckConsistency_BothWarnings_WhenFiberExceedsCarbsAndCaloriesMismatch()
    {
        // Fiber > carbs AND calorie mismatch
        var hints = NutritionHints.CheckConsistency(
            calories: 900m,
            proteinGrams: 20m,
            carbsGrams: 5m,  // carbs 5g
            fatGrams: 10m,
            fiberGrams: 20m); // fiber 20g > 5g carbs

        hints.Should().HaveCount(2);
        hints.Should().Contain(h => h.Contains("Fiber") && h.Contains("carbohydrate"));
        hints.Should().Contain(h => h.Contains("macro-based estimate"));
    }

    [Fact]
    public void CheckConsistency_NoWarning_WhenAllNullValues()
    {
        var hints = NutritionHints.CheckConsistency(null, null, null, null, null);

        hints.Should().BeEmpty();
    }

    [Fact]
    public void CheckConsistency_NoWarning_WhenMacrosProvidedButNoCalories()
    {
        // Can't compute Atwater check without calories
        var hints = NutritionHints.CheckConsistency(
            calories: null,
            proteinGrams: 20m,
            carbsGrams: 30m,
            fatGrams: 10m,
            fiberGrams: 5m);

        hints.Should().BeEmpty();
    }

    [Fact]
    public void CheckConsistency_NoWarning_WhenOnlyCaloriesProvided()
    {
        // Can't compute Atwater check without macros
        var hints = NutritionHints.CheckConsistency(
            calories: 500m,
            proteinGrams: null,
            carbsGrams: null,
            fatGrams: null,
            fiberGrams: null);

        hints.Should().BeEmpty();
    }

    [Fact]
    public void CheckConsistency_NoWarning_WhenAllZeros()
    {
        var hints = NutritionHints.CheckConsistency(
            calories: 0m,
            proteinGrams: 0m,
            carbsGrams: 0m,
            fatGrams: 0m,
            fiberGrams: 0m);

        hints.Should().BeEmpty();
    }

    [Fact]
    public void CheckConsistency_HintContainsActualValues()
    {
        var hints = NutritionHints.CheckConsistency(
            calories: 600m,
            proteinGrams: 20m,
            carbsGrams: 30m,
            fatGrams: 10m,
            fiberGrams: null);

        // Atwater = 290, entered 600
        hints.Should().ContainSingle();
        hints[0].Should().Contain("600");
        hints[0].Should().Contain("290");
    }

    [Fact]
    public void DeviationThreshold_Is20Percent()
    {
        NutritionHints.DeviationThreshold.Should().Be(0.20m);
    }
}
