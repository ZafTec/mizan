using Microsoft.EntityFrameworkCore;
using Mizan.Domain.Entities;

namespace Mizan.Application.Interfaces;

public interface IMizanDbContext
{
    // Auth tables - Users is read-only (managed by frontend)
    DbSet<User> Users { get; }
    // Accounts, Sessions - REMOVED (managed by frontend)

    // Business tables (managed by backend)
    DbSet<Household> Households { get; }
    DbSet<HouseholdMember> HouseholdMembers { get; }
    DbSet<Food> Foods { get; }
    DbSet<Recipe> Recipes { get; }
    DbSet<RecipeIngredient> RecipeIngredients { get; }
    DbSet<RecipeInstruction> RecipeInstructions { get; }
    DbSet<RecipeNutrition> RecipeNutritions { get; }
    DbSet<RecipeTag> RecipeTags { get; }
    DbSet<FavoriteRecipe> FavoriteRecipes { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<FoodDiaryEntry> FoodDiaryEntries { get; }
    DbSet<MealPlan> MealPlans { get; }
    DbSet<MealPlanRecipe> MealPlanRecipes { get; }
    DbSet<ShoppingList> ShoppingLists { get; }
    DbSet<ShoppingListItem> ShoppingListItems { get; }
    DbSet<UserGoal> UserGoals { get; }
    DbSet<GoalProgress> GoalProgress { get; }
    DbSet<Exercise> Exercises { get; }
    DbSet<Workout> Workouts { get; }
    DbSet<WorkoutExercise> WorkoutExercises { get; }
    DbSet<ExerciseSet> ExerciseSets { get; }
    DbSet<BodyMeasurement> BodyMeasurements { get; }
    DbSet<TrainerClientRelationship> TrainerClientRelationships { get; }
    DbSet<ChatConversation> ChatConversations { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<Achievement> Achievements { get; }
    DbSet<UserAchievement> UserAchievements { get; }
    DbSet<Streak> Streaks { get; }
    DbSet<AiChatThread> AiChatThreads { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
