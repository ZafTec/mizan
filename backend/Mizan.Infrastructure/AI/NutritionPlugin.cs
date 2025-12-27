using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.AI;

public class NutritionPlugin
{
    private readonly IMizanDbContext _context;
    private readonly Guid _userId;

    public NutritionPlugin(IMizanDbContext context, Guid userId)
    {
        _context = context;
        _userId = userId;
    }

    [KernelFunction, Description("Log a food item eaten by the user")]
    public async Task<string> LogFood(
        [Description("Name of the food")] string foodName,
        [Description("Portion size in grams")] double portionGrams,
        [Description("Meal type (breakfast, lunch, dinner, or snack)")] string mealType)
    {
        // Try to find the food in the database
        var food = await _context.Foods
            .FirstOrDefaultAsync(f => f.Name.ToLower().Contains(foodName.ToLower()));

        if (food == null)
        {
            return $"Food '{foodName}' not found in database. Please add it first or try a different name.";
        }

        var servingMultiplier = (decimal)portionGrams / 100m;
        var calories = (int)(food.CaloriesPer100g * servingMultiplier);
        var protein = food.ProteinPer100g * servingMultiplier;
        var carbs = food.CarbsPer100g * servingMultiplier;
        var fat = food.FatPer100g * servingMultiplier;

        var entry = new FoodDiaryEntry
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            FoodId = food.Id,
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MealType = mealType.ToLower(),
            Servings = servingMultiplier,
            Calories = calories,
            ProteinGrams = protein,
            CarbsGrams = carbs,
            FatGrams = fat,
            LoggedAt = DateTime.UtcNow
        };

        _context.FoodDiaryEntries.Add(entry);
        await _context.SaveChangesAsync();

        return $"Logged {portionGrams}g of {food.Name} for {mealType}: {calories} kcal, {protein:F1}g protein, {carbs:F1}g carbs, {fat:F1}g fat";
    }

    [KernelFunction, Description("Get nutrition information for a food item")]
    public async Task<string> GetNutritionInfo(
        [Description("Name of the food to look up")] string foodName)
    {
        var food = await _context.Foods
            .FirstOrDefaultAsync(f => f.Name.ToLower().Contains(foodName.ToLower()));

        if (food == null)
        {
            return $"Food '{foodName}' not found in database.";
        }

        return $@"Nutrition info for {food.Name} (per 100g):
- Calories: {food.CaloriesPer100g} kcal
- Protein: {food.ProteinPer100g}g
- Carbs: {food.CarbsPer100g}g
- Fat: {food.FatPer100g}g
- Fiber: {food.FiberPer100g?.ToString("F1") ?? "N/A"}g
- Sugar: {food.SugarPer100g?.ToString("F1") ?? "N/A"}g
- Serving size: {food.ServingSize}{food.ServingUnit}";
    }

    [KernelFunction, Description("Get the user's daily nutrition summary for a specific date")]
    public async Task<string> GetDailySummary(
        [Description("Date in yyyy-MM-dd format (defaults to today if not provided)")] string? date = null)
    {
        var targetDate = string.IsNullOrEmpty(date)
            ? DateOnly.FromDateTime(DateTime.UtcNow)
            : DateOnly.Parse(date);

        var entries = await _context.FoodDiaryEntries
            .Where(e => e.UserId == _userId && e.EntryDate == targetDate)
            .ToListAsync();

        if (!entries.Any())
        {
            return $"No food logged for {targetDate:MMM dd, yyyy}.";
        }

        var totalCalories = entries.Sum(e => e.Calories ?? 0);
        var totalProtein = entries.Sum(e => e.ProteinGrams ?? 0);
        var totalCarbs = entries.Sum(e => e.CarbsGrams ?? 0);
        var totalFat = entries.Sum(e => e.FatGrams ?? 0);

        // Get user's goals
        var goal = await _context.UserGoals
            .FirstOrDefaultAsync(g => g.UserId == _userId && g.IsActive);

        var summary = $@"Daily Summary for {targetDate:MMM dd, yyyy}:
- Total Calories: {totalCalories} kcal
- Protein: {totalProtein:F1}g
- Carbs: {totalCarbs:F1}g
- Fat: {totalFat:F1}g
- Items logged: {entries.Count}";

        if (goal != null)
        {
            var calRemaining = (goal.TargetCalories ?? 0) - totalCalories;
            var protRemaining = (goal.TargetProteinGrams ?? 0) - totalProtein;

            summary += $@"

Goals Progress:
- Calories: {totalCalories}/{goal.TargetCalories} ({(calRemaining > 0 ? $"{calRemaining} remaining" : "target reached!")})
- Protein: {totalProtein:F1}/{goal.TargetProteinGrams}g ({(protRemaining > 0 ? $"{protRemaining:F1}g remaining" : "target reached!")})";
        }

        return summary;
    }

    [KernelFunction, Description("Get the user's nutrition goals")]
    public async Task<string> GetNutritionGoals()
    {
        var goal = await _context.UserGoals
            .FirstOrDefaultAsync(g => g.UserId == _userId && g.IsActive);

        if (goal == null)
        {
            return "No nutrition goals set. Consider setting daily targets for calories, protein, carbs, and fat.";
        }

        return $@"Current Nutrition Goals:
- Goal Type: {goal.GoalType ?? "General"}
- Daily Calories: {goal.TargetCalories} kcal
- Daily Protein: {goal.TargetProteinGrams}g
- Daily Carbs: {goal.TargetCarbsGrams}g
- Daily Fat: {goal.TargetFatGrams}g
- Target Weight: {(goal.TargetWeight.HasValue ? $"{goal.TargetWeight} {goal.WeightUnit}" : "Not set")}";
    }

    [KernelFunction, Description("Get recipe suggestions based on remaining macros for the day")]
    public async Task<string> GetRecipeSuggestions(
        [Description("Maximum calories per serving for suggested recipes")] int? maxCalories = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var todayEntries = await _context.FoodDiaryEntries
            .Where(e => e.UserId == _userId && e.EntryDate == today)
            .ToListAsync();

        var goal = await _context.UserGoals
            .FirstOrDefaultAsync(g => g.UserId == _userId && g.IsActive);

        var consumedCalories = todayEntries.Sum(e => e.Calories ?? 0);
        var remainingCalories = (goal?.TargetCalories ?? 2000) - consumedCalories;
        var targetMaxCal = maxCalories ?? Math.Min(remainingCalories, 800);

        var recipes = await _context.Recipes
            .Include(r => r.Nutrition)
            .Where(r => r.IsPublic || r.UserId == _userId)
            .Where(r => r.Nutrition != null && r.Nutrition.CaloriesPerServing <= targetMaxCal)
            .OrderBy(r => Guid.NewGuid()) // Random order
            .Take(5)
            .ToListAsync();

        if (!recipes.Any())
        {
            return "No recipes found matching your criteria.";
        }

        var suggestions = "Recipe Suggestions:\n" + string.Join("\n", recipes.Select(r =>
            $"- {r.Title}: {r.Nutrition!.CaloriesPerServing} kcal, {r.Nutrition.ProteinGrams}g protein ({r.Servings} servings)"));

        return suggestions + $"\n\nRemaining calories for today: {remainingCalories} kcal";
    }
}
