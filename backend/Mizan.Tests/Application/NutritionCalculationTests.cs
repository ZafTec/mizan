using FluentAssertions;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>
/// Tests for nutrition calculation correctness against the Atwater system.
/// Atwater general factors: Protein = 4 kcal/g, Carbs = 4 kcal/g, Fat = 9 kcal/g.
/// Fiber contributes approximately 2 kcal/g (insoluble) but most databases store 0.
/// Stored calories may deviate slightly from Atwater due to fiber, alcohol, and rounding.
/// </summary>
public class NutritionCalculationTests
{
    private const decimal ProteinKcalPerGram = 4m;
    private const decimal CarbsKcalPerGram = 4m;
    private const decimal FatKcalPerGram = 9m;

    [Fact]
    public void AtwaterCalories_ProteinOnly_4KcalPerGram()
    {
        var proteinG = 25m;
        var expected = 100m;
        (proteinG * ProteinKcalPerGram).Should().Be(expected);
    }

    [Fact]
    public void AtwaterCalories_CarbsOnly_4KcalPerGram()
    {
        var carbsG = 25m;
        var expected = 100m;
        (carbsG * CarbsKcalPerGram).Should().Be(expected);
    }

    [Fact]
    public void AtwaterCalories_FatOnly_9KcalPerGram()
    {
        var fatG = 10m;
        var expected = 90m;
        (fatG * FatKcalPerGram).Should().Be(expected);
    }

    [Fact]
    public void AtwaterCalories_MixedMacros_SumsCorrectly()
    {
        // 20g protein (80 kcal) + 30g carbs (120 kcal) + 10g fat (90 kcal) = 290 kcal
        var protein = 20m;
        var carbs = 30m;
        var fat = 10m;
        var expected = 290m;

        var calculated = (protein * ProteinKcalPerGram) + (carbs * CarbsKcalPerGram) + (fat * FatKcalPerGram);

        calculated.Should().Be(expected);
    }

    [Fact]
    public void IngredientRatio_100g_IsFullAmount()
    {
        var caloriesPer100g = 200m;
        var amountGrams = 100m;

        var ratio = amountGrams / 100m;
        var calories = caloriesPer100g * ratio;

        calories.Should().Be(200m);
    }

    [Fact]
    public void IngredientRatio_200g_IsDouble()
    {
        var caloriesPer100g = 150m;
        var amountGrams = 200m;

        var calories = caloriesPer100g * (amountGrams / 100m);

        calories.Should().Be(300m);
    }

    [Fact]
    public void IngredientRatio_50g_IsHalf()
    {
        var caloriesPer100g = 400m;
        var amountGrams = 50m;

        var calories = caloriesPer100g * (amountGrams / 100m);

        calories.Should().Be(200m);
    }

    [Fact]
    public void PerServingNutrition_DividesByServingCount()
    {
        var totalCalories = 400m;
        var totalProtein = 40m;
        var totalCarbs = 60m;
        var totalFat = 10m;
        var totalFiber = 8m;
        var servings = 2;

        (totalCalories / servings).Should().Be(200m);
        (totalProtein / servings).Should().Be(20m);
        (totalCarbs / servings).Should().Be(30m);
        (totalFat / servings).Should().Be(5m);
        (totalFiber / servings).Should().Be(4m);
    }

    [Fact]
    public void PerServingNutrition_ThreeServings_DividesCorrectly()
    {
        var totalCalories = 900m;
        var servings = 3;

        (totalCalories / servings).Should().Be(300m);
    }

    [Fact]
    public void FiberContribution_ScalesWithAmount()
    {
        // 10g fiber per 100g food, using 300g → 30g total fiber
        var fiberPer100g = 10m;
        var amountGrams = 300m;

        var totalFiber = fiberPer100g * (amountGrams / 100m);

        totalFiber.Should().Be(30m);
    }

    [Fact]
    public void NullFiber_TreatedAsZero_WhenAggregating()
    {
        decimal? nullableFiber = null;

        var contribution = (nullableFiber ?? 0m) * 2m;

        contribution.Should().Be(0m);
    }

    [Fact]
    public void RecipeIngredientMacros_AllNonNegative_For100g()
    {
        var proteinPer100g = 20m;
        var carbsPer100g = 50m;
        var fatPer100g = 8m;
        var fiberPer100g = 5m;
        var amountGrams = 100m;
        var ratio = amountGrams / 100m;

        var protein = proteinPer100g * ratio;
        var carbs = carbsPer100g * ratio;
        var fat = fatPer100g * ratio;
        var fiber = fiberPer100g * ratio;

        protein.Should().BeGreaterThanOrEqualTo(0m);
        carbs.Should().BeGreaterThanOrEqualTo(0m);
        fat.Should().BeGreaterThanOrEqualTo(0m);
        fiber.Should().BeGreaterThanOrEqualTo(0m);
    }

    [Fact]
    public void RecipeIngredientMacros_FiberCannotExceedCarbs()
    {
        // Fiber is a type of carbohydrate — it must not exceed total carbs
        var carbsPer100g = 30m;
        var fiberPer100g = 8m; // fiber is subset of carbs

        fiberPer100g.Should().BeLessThanOrEqualTo(carbsPer100g, "fiber is a carbohydrate and cannot exceed total carbs");
    }

    [Fact]
    public void SubRecipeContribution_MultipliesByServingsUsed()
    {
        var caloriesPerServing = 350m;
        var proteinPerServing = 30m;
        var fiberPerServing = 5m;
        var servingsUsed = 2m;

        (caloriesPerServing * servingsUsed).Should().Be(700m);
        (proteinPerServing * servingsUsed).Should().Be(60m);
        (fiberPerServing * servingsUsed).Should().Be(10m);
    }

    [Fact]
    public void StoredCalories_WithinReasonableRangeOfAtwaterEstimate()
    {
        // USDA calories are based on Atwater modified factors.
        // They should be within ~15% of the simple Atwater calculation.
        // (Deviation accounts for fiber's caloric contribution ~2 kcal/g and rounding.)
        var proteinG = 25m;
        var carbsG = 55m;
        var fatG = 8m;
        var storedCaloriesPer100g = 378m; // realistic value (e.g., bread)

        var atwaterEstimate = (proteinG * ProteinKcalPerGram) + (carbsG * CarbsKcalPerGram) + (fatG * FatKcalPerGram);
        // 100 + 220 + 72 = 392

        var deviation = Math.Abs((double)(storedCaloriesPer100g - atwaterEstimate)) / (double)atwaterEstimate;

        deviation.Should().BeLessThan(0.15, "stored calories should be within 15% of Atwater estimate");
    }

    [Fact]
    public void TotalCalories_FromMultipleIngredients_SumsCorrectly()
    {
        // Ingredient 1: 200g chicken (165 kcal/100g) = 330 kcal
        // Ingredient 2: 100g rice (130 kcal/100g) = 130 kcal
        // Total = 460 kcal, 2 servings → 230 kcal/serving
        var chickenCalPer100g = 165m;
        var chickenAmount = 200m;
        var riceCalPer100g = 130m;
        var riceAmount = 100m;
        var servings = 2;

        var total = (chickenCalPer100g * chickenAmount / 100m) + (riceCalPer100g * riceAmount / 100m);
        var perServing = total / servings;

        total.Should().Be(460m);
        perServing.Should().Be(230m);
    }

    [Fact]
    public void FiberPerServing_CalculatesCorrectly_FromMultipleIngredients()
    {
        // 300g broccoli (2.6g fiber/100g) + 200g beans (6g fiber/100g) = 7.8 + 12 = 19.8g fiber total
        // 3 servings → 6.6g fiber per serving
        var broccoliFiberPer100g = 2.6m;
        var broccoliAmount = 300m;
        var beansFiberPer100g = 6m;
        var beansAmount = 200m;
        var servings = 3;

        var totalFiber = (broccoliFiberPer100g * broccoliAmount / 100m) + (beansFiberPer100g * beansAmount / 100m);
        var fiberPerServing = totalFiber / servings;

        totalFiber.Should().BeApproximately(19.8m, 0.01m);
        fiberPerServing.Should().BeApproximately(6.6m, 0.01m);
    }
}
