using FluentAssertions;
using Mizan.Domain.Goals;
using Xunit;

namespace Mizan.Tests.GoalRules;

public class GoalHintsTests
{
    [Fact]
    public void CheckGoalSanity_NoWarnings_WhenReasonableGoal()
    {
        // 2000 kcal with balanced macros: 50g protein (200), 250g carbs (1000), 55g fat (495) = 1695 kcal
        var hints = GoalHints.CheckGoalSanity(
            goalType: "maintenance",
            targetCalories: 2000,
            targetProteinGrams: 50m,
            targetCarbsGrams: 250m,
            targetFatGrams: 55m,
            targetDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));

        hints.Should().BeEmpty();
    }

    [Fact]
    public void CheckGoalSanity_Warning_WhenTargetDateInPast()
    {
        var hints = GoalHints.CheckGoalSanity(
            goalType: "weight_loss",
            targetCalories: 1800,
            targetProteinGrams: null,
            targetCarbsGrams: null,
            targetFatGrams: null,
            targetDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));

        hints.Should().ContainSingle(h => h.Contains("future"));
    }

    [Fact]
    public void CheckGoalSanity_Warning_WhenCaloriesTooLow()
    {
        var hints = GoalHints.CheckGoalSanity(
            goalType: "weight_loss",
            targetCalories: 500,
            targetProteinGrams: null,
            targetCarbsGrams: null,
            targetFatGrams: null,
            targetDate: null);

        hints.Should().ContainSingle(h => h.Contains("very low") && h.Contains("800"));
    }

    [Fact]
    public void CheckGoalSanity_Warning_WhenCaloriesTooHigh()
    {
        var hints = GoalHints.CheckGoalSanity(
            goalType: "maintenance",
            targetCalories: 6000,
            targetProteinGrams: null,
            targetCarbsGrams: null,
            targetFatGrams: null,
            targetDate: null);

        hints.Should().ContainSingle(h => h.Contains("very high") && h.Contains("5000"));
    }

    [Fact]
    public void CheckGoalSanity_Warning_WhenMacrosNotAlignedWithCalories()
    {
        // Macros estimate ~1000 kcal but target is 2000 kcal
        var hints = GoalHints.CheckGoalSanity(
            goalType: "maintenance",
            targetCalories: 2000,
            targetProteinGrams: 50m,  // 200 kcal
            targetCarbsGrams: 100m,   // 400 kcal
            targetFatGrams: 50m,      // 450 kcal (total ~1050)
            targetDate: null);

        hints.Should().ContainSingle(h => h.Contains("differ significantly") || h.Contains("consistent"));
    }

    [Fact]
    public void CheckGoalSanity_Warning_WhenWeightLossCaloriesTooHigh()
    {
        var hints = GoalHints.CheckGoalSanity(
            goalType: "weight_loss",
            targetCalories: 4000,
            targetProteinGrams: null,
            targetCarbsGrams: null,
            targetFatGrams: null,
            targetDate: null);

        hints.Should().ContainSingle(h => h.Contains("weight loss") && h.Contains("above typical"));
    }

    [Fact]
    public void CheckGoalSanity_Warning_WhenMuscleGainProteinTooLow()
    {
        // 2000 kcal with only 40g protein (160 kcal = 8%)
        var hints = GoalHints.CheckGoalSanity(
            goalType: "muscle_gain",
            targetCalories: 2000,
            targetProteinGrams: 40m,
            targetCarbsGrams: 250m,
            targetFatGrams: 60m,
            targetDate: null);

        hints.Should().ContainSingle(h => h.Contains("muscle gain") && h.Contains("protein"));
    }

    [Fact]
    public void CheckGoalSanity_NoWarning_WhenMuscleGainProteinAdequate()
    {
        // 2000 kcal with 150g protein (600 kcal = 30%)
        var hints = GoalHints.CheckGoalSanity(
            goalType: "muscle_gain",
            targetCalories: 2000,
            targetProteinGrams: 150m,
            targetCarbsGrams: 200m,
            targetFatGrams: 65m,
            targetDate: null);

        hints.Should().BeEmpty();
    }

    [Fact]
    public void CheckGoalSanity_Warning_WhenProteinExcessivelyHigh()
    {
        // 2000 kcal with 300g protein (1200 kcal = 60%)
        var hints = GoalHints.CheckGoalSanity(
            goalType: "maintenance",
            targetCalories: 2000,
            targetProteinGrams: 300m,
            targetCarbsGrams: 100m,
            targetFatGrams: 50m,
            targetDate: null);

        hints.Should().ContainSingle(h => h.Contains("protein") && h.Contains("40%"));
    }

    [Fact]
    public void CheckGoalSanity_Warning_WhenCarbsExcessivelyHigh()
    {
        // 2000 kcal with 400g carbs (1600 kcal = 80%)
        var hints = GoalHints.CheckGoalSanity(
            goalType: "maintenance",
            targetCalories: 2000,
            targetProteinGrams: 50m,
            targetCarbsGrams: 400m,
            targetFatGrams: 30m,
            targetDate: null);

        hints.Should().ContainSingle(h => h.Contains("Carbs") && h.Contains("70%"));
    }

    [Fact]
    public void CheckGoalSanity_Warning_WhenFatTooLow()
    {
        // 2000 kcal with only 20g fat (180 kcal = 9%)
        var hints = GoalHints.CheckGoalSanity(
            goalType: "maintenance",
            targetCalories: 2000,
            targetProteinGrams: 50m,
            targetCarbsGrams: 350m,
            targetFatGrams: 20m,
            targetDate: null);

        hints.Should().ContainSingle(h => h.Contains("Fat") && h.Contains("15%"));
    }

    [Fact]
    public void CheckGoalSanity_MultipleWarnings_WhenMultipleIssues()
    {
        // Target date in past + calories too low + macros misaligned
        var hints = GoalHints.CheckGoalSanity(
            goalType: "weight_loss",
            targetCalories: 600,
            targetProteinGrams: 50m,  // 200 kcal (33%)
            targetCarbsGrams: 50m,    // 200 kcal (33%)
            targetFatGrams: 50m,      // 450 kcal (75%): extremely high
            targetDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)));

        hints.Should().HaveCountGreaterThan(1);
        hints.Should().Contain(h => h.Contains("past"));
        hints.Should().Contain(h => h.Contains("very low"));
    }

    [Fact]
    public void CheckGoalSanity_NoWarnings_WhenAllNull()
    {
        var hints = GoalHints.CheckGoalSanity(null, null, null, null, null, null);
        hints.Should().BeEmpty();
    }

    [Fact]
    public void CheckGoalSanity_NoWarning_WhenOnlyCaloriesProvided()
    {
        // Can't validate without macros
        var hints = GoalHints.CheckGoalSanity(
            goalType: null,
            targetCalories: 2000,
            targetProteinGrams: null,
            targetCarbsGrams: null,
            targetFatGrams: null,
            targetDate: null);

        hints.Should().BeEmpty();
    }

    [Fact]
    public void CheckGoalSanity_NoWarning_WhenPartialMacrosProvided()
    {
        // Can't fully validate consistency with only some macros
        var hints = GoalHints.CheckGoalSanity(
            goalType: "maintenance",
            targetCalories: 2000,
            targetProteinGrams: 100m,
            targetCarbsGrams: 200m,
            targetFatGrams: null,
            targetDate: null);

        hints.Should().BeEmpty();
    }

    [Fact]
    public void CheckGoalSanity_MacroImbalance_WarnsWhenMacrosDontMatchCalories()
    {
        // 200g protein (800) + 100g carbs (400) + 20g fat (180) = 1380 kcal vs target 2000, 44.9% deviation
        var hints = GoalHints.CheckGoalSanity(
            goalType: "maintenance",
            targetCalories: 2000,
            targetProteinGrams: 200m,
            targetCarbsGrams: 100m,
            targetFatGrams: 20m,
            targetDate: null);

        hints.Should().ContainSingle(h => h.Contains("differ significantly"));
    }

    [Fact]
    public void CheckGoalSanity_HintTextIncludesActualValues()
    {
        var hints = GoalHints.CheckGoalSanity(
            goalType: null,
            targetCalories: 700,
            targetProteinGrams: null,
            targetCarbsGrams: null,
            targetFatGrams: null,
            targetDate: null);

        hints.Should().ContainSingle();
        hints[0].Should().Contain("700");
        hints[0].Should().Contain("800");
    }

    // Each macro's share of the calories, with the others left out: protein and carbs over their
    // ceilings warn, and fat now does too, so the rule is the same for every macro.
    [Theory]
    [InlineData(2000, 600d, null, null, "Protein")]   // 120%
    [InlineData(2000, null, 600d, null, "Carbs")]     // 120%
    [InlineData(2000, null, null, 200d, "Fat")]       // 90%
    [InlineData(2000, null, null, 110d, "Fat")]       // 49.5%
    [InlineData(2000, 175d, null, null, null)]        // 35%
    [InlineData(2000, null, 300d, null, null)]        // 60%
    [InlineData(2000, null, null, 70d, null)]         // 31.5%
    [InlineData(2000, null, null, 20d, null)]         // 9%, but a floor needs all three macros set
    public void EachMacro_IsHeldToItsShareOfCalories(int calories, double? protein, double? carbs, double? fat, string? warned)
    {
        var hints = GoalHints.CheckGoalSanity(
            "general", calories, (decimal?)protein, (decimal?)carbs, (decimal?)fat, targetDate: null);

        var shares = hints.Where(h => h.Contains("of your calories")).ToList();
        if (warned is null) shares.Should().BeEmpty();
        else shares.Should().ContainSingle().Which.Should().StartWith(warned);
    }
}
