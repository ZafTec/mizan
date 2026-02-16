using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Data;

public class MizanDbContext : DbContext, IMizanDbContext
{
    public MizanDbContext(DbContextOptions<MizanDbContext> options) : base(options)
    {
    }

    // BetterAuth core tables (managed by frontend Drizzle)
    // User is read-only for backend (excluded from migrations via SetIsTableExcludedFromMigrations)
    public DbSet<User> Users => Set<User>();
    // Account, Session, Jwk, Verification - REMOVED (managed entirely by frontend)

    // Household/Organization
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();

    // Food & Nutrition
    public DbSet<Food> Foods => Set<Food>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeInstruction> RecipeInstructions => Set<RecipeInstruction>();
    public DbSet<RecipeNutrition> RecipeNutritions => Set<RecipeNutrition>();
    public DbSet<RecipeTag> RecipeTags => Set<RecipeTag>();
    public DbSet<FavoriteRecipe> FavoriteRecipes => Set<FavoriteRecipe>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<FoodDiaryEntry> FoodDiaryEntries => Set<FoodDiaryEntry>();

    // Meal Planning
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<MealPlanRecipe> MealPlanRecipes => Set<MealPlanRecipe>();
    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();

    // Goals
    public DbSet<UserGoal> UserGoals => Set<UserGoal>();
    public DbSet<GoalProgress> GoalProgress => Set<GoalProgress>();

    // Fitness
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<Workout> Workouts => Set<Workout>();
    public DbSet<WorkoutExercise> WorkoutExercises => Set<WorkoutExercise>();
    public DbSet<ExerciseSet> ExerciseSets => Set<ExerciseSet>();
    public DbSet<BodyMeasurement> BodyMeasurements => Set<BodyMeasurement>();

    // Trainer/Client
    public DbSet<TrainerClientRelationship> TrainerClientRelationships => Set<TrainerClientRelationship>();
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    // Gamification
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<Streak> Streaks => Set<Streak>();

    // AI
    public DbSet<AiChatThread> AiChatThreads => Set<AiChatThread>();

    // MCP Integration
    public DbSet<McpToken> McpTokens => Set<McpToken>();
    public DbSet<McpUsageLog> McpUsageLogs => Set<McpUsageLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration (READ-ONLY - managed by frontend Drizzle, excluded from backend migrations)
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.EmailVerified).HasColumnName("email_verified");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Image).HasColumnName("image");
            entity.Property(e => e.Role).HasColumnName("role");
            entity.Property(e => e.Banned).HasColumnName("banned");
            entity.Property(e => e.BanReason).HasColumnName("ban_reason");
            entity.Property(e => e.BanExpires).HasColumnName("ban_expires");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            // CRITICAL: Exclude from migrations - table managed by frontend
            entity.Metadata.SetIsTableExcludedFromMigrations(true);
        });

        // AUTH TABLES (Account, Session, Jwk, Verification) - REMOVED
        // These are managed entirely by frontend Drizzle ORM
        // Backend does not configure or migrate these tables

        // Household configuration
        modelBuilder.Entity<Household>(entity =>
        {
            entity.ToTable("households");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        });

        // HouseholdMember configuration
        modelBuilder.Entity<HouseholdMember>(entity =>
        {
            entity.ToTable("household_members");
            entity.HasKey(e => new { e.HouseholdId, e.UserId });
            entity.Property(e => e.HouseholdId).HasColumnName("household_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Role).HasColumnName("role").HasMaxLength(20).HasDefaultValue("member");
            entity.Property(e => e.CanEditRecipes).HasColumnName("can_edit_recipes").HasDefaultValue(true);
            entity.Property(e => e.CanEditShoppingList).HasColumnName("can_edit_shopping_list").HasDefaultValue(true);
            entity.Property(e => e.CanViewNutrition).HasColumnName("can_view_nutrition").HasDefaultValue(false);
            entity.Property(e => e.JoinedAt).HasColumnName("joined_at").HasDefaultValueSql("NOW()");
            entity.HasOne(e => e.Household).WithMany(h => h.Members).HasForeignKey(e => e.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User).WithMany(u => u.HouseholdMemberships).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // Food configuration
        modelBuilder.Entity<Food>(entity =>
        {
            entity.ToTable("foods");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Brand).HasColumnName("brand").HasMaxLength(255);
            entity.Property(e => e.Barcode).HasColumnName("barcode").HasMaxLength(100);
            entity.Property(e => e.ServingSize).HasColumnName("serving_size").HasPrecision(10, 2).HasDefaultValue(100m);
            entity.Property(e => e.ServingUnit).HasColumnName("serving_unit").HasMaxLength(50).HasDefaultValue("g");
            entity.Property(e => e.CaloriesPer100g).HasColumnName("calories_per_100g").HasPrecision(8, 2);
            entity.Property(e => e.ProteinPer100g).HasColumnName("protein_per_100g").HasPrecision(8, 2);
            entity.Property(e => e.CarbsPer100g).HasColumnName("carbs_per_100g").HasPrecision(8, 2);
            entity.Property(e => e.FatPer100g).HasColumnName("fat_per_100g").HasPrecision(8, 2);
            entity.Property(e => e.FiberPer100g).HasColumnName("fiber_per_100g").HasPrecision(8, 2);
            entity.Property(e => e.SugarPer100g).HasColumnName("sugar_per_100g").HasPrecision(8, 2);
            entity.Property(e => e.SodiumPer100g).HasColumnName("sodium_per_100g").HasPrecision(8, 2);
            entity.Property(e => e.IsVerified).HasColumnName("is_verified").HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            entity.HasIndex(e => e.Barcode);
        });

        // Recipe configuration
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.ToTable("recipes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.HouseholdId).HasColumnName("household_id");
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Servings).HasColumnName("servings").HasDefaultValue(1);
            entity.Property(e => e.PrepTimeMinutes).HasColumnName("prep_time_minutes");
            entity.Property(e => e.CookTimeMinutes).HasColumnName("cook_time_minutes");
            entity.Property(e => e.SourceUrl).HasColumnName("source_url");
            entity.Property(e => e.ImageUrl).HasColumnName("image_url");
            entity.Property(e => e.IsPublic).HasColumnName("is_public").HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            entity.HasOne(e => e.User).WithMany(u => u.Recipes).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Household).WithMany(h => h.Recipes).HasForeignKey(e => e.HouseholdId).OnDelete(DeleteBehavior.SetNull);
        });

        // RecipeIngredient configuration
        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.ToTable("recipe_ingredients", t => t.HasCheckConstraint(
                "CK_RecipeIngredient_HasFoodOrSubRecipeOrText",
                "(food_id IS NOT NULL) OR (sub_recipe_id IS NOT NULL) OR (ingredient_text IS NOT NULL AND ingredient_text <> '')"
            ));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.RecipeId).HasColumnName("recipe_id");
            entity.Property(e => e.FoodId).HasColumnName("food_id");
            entity.Property(e => e.SubRecipeId).HasColumnName("sub_recipe_id");
            entity.Property(e => e.IngredientText).HasColumnName("ingredient_text").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Amount).HasColumnName("amount").HasPrecision(10, 2);
            entity.Property(e => e.Unit).HasColumnName("unit").HasMaxLength(50);
            entity.Property(e => e.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            entity.HasOne(e => e.Recipe).WithMany(r => r.Ingredients).HasForeignKey(e => e.RecipeId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Food).WithMany(f => f.RecipeIngredients).HasForeignKey(e => e.FoodId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.SubRecipe).WithMany().HasForeignKey(e => e.SubRecipeId).OnDelete(DeleteBehavior.Restrict);
        });

        // RecipeInstruction configuration
        modelBuilder.Entity<RecipeInstruction>(entity =>
        {
            entity.ToTable("recipe_instructions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.RecipeId).HasColumnName("recipe_id");
            entity.Property(e => e.StepNumber).HasColumnName("step_number");
            entity.Property(e => e.Instruction).HasColumnName("instruction").IsRequired();
            entity.HasOne(e => e.Recipe).WithMany(r => r.Instructions).HasForeignKey(e => e.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });

        // RecipeNutrition configuration
        modelBuilder.Entity<RecipeNutrition>(entity =>
        {
            entity.ToTable("recipe_nutrition");
            entity.HasKey(e => e.RecipeId);
            entity.Property(e => e.RecipeId).HasColumnName("recipe_id");
            entity.Property(e => e.CaloriesPerServing).HasColumnName("calories_per_serving").HasPrecision(8, 2);
            entity.Property(e => e.ProteinGrams).HasColumnName("protein_grams").HasPrecision(8, 2);
            entity.Property(e => e.CarbsGrams).HasColumnName("carbs_grams").HasPrecision(8, 2);
            entity.Property(e => e.FatGrams).HasColumnName("fat_grams").HasPrecision(8, 2);
            entity.Property(e => e.FiberGrams).HasColumnName("fiber_grams").HasPrecision(8, 2);
            entity.Property(e => e.SugarGrams).HasColumnName("sugar_grams").HasPrecision(8, 2);
            entity.Property(e => e.SodiumMg).HasColumnName("sodium_mg").HasPrecision(8, 2);
            entity.HasOne(e => e.Recipe).WithOne(r => r.Nutrition).HasForeignKey<RecipeNutrition>(e => e.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });

        // RecipeTag configuration
        modelBuilder.Entity<RecipeTag>(entity =>
        {
            entity.ToTable("recipe_tags");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.RecipeId).HasColumnName("recipe_id");
            entity.Property(e => e.Tag).HasColumnName("tag").HasMaxLength(50).IsRequired();
            entity.HasOne(e => e.Recipe).WithMany(r => r.Tags).HasForeignKey(e => e.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });

        // FavoriteRecipe configuration
        modelBuilder.Entity<FavoriteRecipe>(entity =>
        {
            entity.ToTable("favorite_recipes");
            entity.HasKey(e => new { e.UserId, e.RecipeId });
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.RecipeId).HasColumnName("recipe_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Recipe).WithMany().HasForeignKey(e => e.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });

        // FoodDiaryEntry configuration
        modelBuilder.Entity<FoodDiaryEntry>(entity =>
        {
            entity.ToTable("food_diary_entries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.FoodId).HasColumnName("food_id");
            entity.Property(e => e.RecipeId).HasColumnName("recipe_id");
            entity.Property(e => e.EntryDate).HasColumnName("entry_date").IsRequired();
            entity.Property(e => e.MealType).HasColumnName("meal_type").HasMaxLength(20);
            entity.Property(e => e.Servings).HasColumnName("servings").HasPrecision(6, 2).HasDefaultValue(1m);
            entity.Property(e => e.Calories).HasColumnName("calories").HasPrecision(8, 2);
            entity.Property(e => e.ProteinGrams).HasColumnName("protein_grams").HasPrecision(8, 2);
            entity.Property(e => e.CarbsGrams).HasColumnName("carbs_grams").HasPrecision(8, 2);
            entity.Property(e => e.FatGrams).HasColumnName("fat_grams").HasPrecision(8, 2);
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).HasDefaultValue("");
            entity.Property(e => e.LoggedAt).HasColumnName("logged_at").HasDefaultValueSql("NOW()");
            entity.HasIndex(e => new { e.UserId, e.EntryDate });
            entity.HasOne(e => e.User).WithMany(u => u.FoodDiaryEntries).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Food).WithMany(f => f.DiaryEntries).HasForeignKey(e => e.FoodId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Recipe).WithMany(r => r.DiaryEntries).HasForeignKey(e => e.RecipeId).OnDelete(DeleteBehavior.SetNull);
        });

        // MealPlan configuration
        modelBuilder.Entity<MealPlan>(entity =>
        {
            entity.ToTable("meal_plans");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.HouseholdId).HasColumnName("household_id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(e => e.StartDate).HasColumnName("start_date").IsRequired();
            entity.Property(e => e.EndDate).HasColumnName("end_date").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Household).WithMany().HasForeignKey(e => e.HouseholdId).OnDelete(DeleteBehavior.SetNull);
        });

        // MealPlanRecipe configuration
        modelBuilder.Entity<MealPlanRecipe>(entity =>
        {
            entity.ToTable("meal_plan_recipes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.MealPlanId).HasColumnName("meal_plan_id");
            entity.Property(e => e.RecipeId).HasColumnName("recipe_id");
            entity.Property(e => e.Date).HasColumnName("date").IsRequired();
            entity.Property(e => e.MealType).HasColumnName("meal_type").HasMaxLength(20);
            entity.Property(e => e.Servings).HasColumnName("servings").HasPrecision(6, 2).HasDefaultValue(1m);
            entity.HasOne(e => e.MealPlan).WithMany(m => m.MealPlanRecipes).HasForeignKey(e => e.MealPlanId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Recipe).WithMany(r => r.MealPlanRecipes).HasForeignKey(e => e.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });

        // ShoppingList configuration
        modelBuilder.Entity<ShoppingList>(entity =>
        {
            entity.ToTable("shopping_lists");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.HouseholdId).HasColumnName("household_id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Household).WithMany(h => h.ShoppingLists).HasForeignKey(e => e.HouseholdId).OnDelete(DeleteBehavior.SetNull);
        });

        // ShoppingListItem configuration
        modelBuilder.Entity<ShoppingListItem>(entity =>
        {
            entity.ToTable("shopping_list_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.ShoppingListId).HasColumnName("shopping_list_id");
            entity.Property(e => e.FoodId).HasColumnName("food_id");
            entity.Property(e => e.ItemName).HasColumnName("item_name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Amount).HasColumnName("amount").HasPrecision(10, 2);
            entity.Property(e => e.Unit).HasColumnName("unit").HasMaxLength(50);
            entity.Property(e => e.Category).HasColumnName("category").HasMaxLength(100);
            entity.Property(e => e.IsChecked).HasColumnName("is_checked").HasDefaultValue(false);
            entity.HasOne(e => e.ShoppingList).WithMany(s => s.Items).HasForeignKey(e => e.ShoppingListId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Food).WithMany().HasForeignKey(e => e.FoodId).OnDelete(DeleteBehavior.SetNull);
        });

        // UserGoal configuration
        modelBuilder.Entity<UserGoal>(entity =>
        {
            entity.ToTable("user_goals");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.GoalType).HasColumnName("goal_type").HasMaxLength(50);
            entity.Property(e => e.TargetCalories).HasColumnName("target_calories").HasPrecision(8, 2);
            entity.Property(e => e.TargetProteinGrams).HasColumnName("target_protein_grams").HasPrecision(8, 2);
            entity.Property(e => e.TargetCarbsGrams).HasColumnName("target_carbs_grams").HasPrecision(8, 2);
            entity.Property(e => e.TargetFatGrams).HasColumnName("target_fat_grams").HasPrecision(8, 2);
            entity.Property(e => e.TargetWeight).HasColumnName("target_weight").HasPrecision(6, 2);
            entity.Property(e => e.WeightUnit).HasColumnName("weight_unit").HasMaxLength(10).HasDefaultValue("kg");
            entity.Property(e => e.TargetDate).HasColumnName("target_date");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            entity.HasOne(e => e.User).WithMany(u => u.Goals).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // GoalProgress configuration
        modelBuilder.Entity<GoalProgress>(entity =>
        {
            entity.ToTable("goal_progress");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.UserGoalId).HasColumnName("user_goal_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ActualCalories).HasColumnName("actual_calories").HasPrecision(8, 2);
            entity.Property(e => e.ActualProteinGrams).HasColumnName("actual_protein_grams").HasPrecision(6, 2);
            entity.Property(e => e.ActualCarbsGrams).HasColumnName("actual_carbs_grams").HasPrecision(6, 2);
            entity.Property(e => e.ActualFatGrams).HasColumnName("actual_fat_grams").HasPrecision(6, 2);
            entity.Property(e => e.ActualWeight).HasColumnName("actual_weight").HasPrecision(6, 2);
            entity.Property(e => e.Date).HasColumnName("date");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.HasIndex(e => new { e.UserId, e.Date }).IsUnique();
            entity.HasIndex(e => e.UserGoalId);
            entity.HasIndex(e => e.Date);
            entity.HasOne(e => e.UserGoal).WithMany().HasForeignKey(e => e.UserGoalId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // Exercise configuration
        modelBuilder.Entity<Exercise>(entity =>
        {
            entity.ToTable("exercises");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Category).HasColumnName("category").HasMaxLength(50).IsRequired();
            entity.Property(e => e.MuscleGroup).HasColumnName("muscle_group").HasMaxLength(100);
            entity.Property(e => e.Equipment).HasColumnName("equipment").HasMaxLength(100);
            entity.Property(e => e.VideoUrl).HasColumnName("video_url");
            entity.Property(e => e.ImageUrl).HasColumnName("image_url");
            entity.Property(e => e.IsCustom).HasColumnName("is_custom").HasDefaultValue(false);
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        // Workout configuration
        modelBuilder.Entity<Workout>(entity =>
        {
            entity.ToTable("workouts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(e => e.WorkoutDate).HasColumnName("workout_date").IsRequired();
            entity.Property(e => e.DurationMinutes).HasColumnName("duration_minutes");
            entity.Property(e => e.CaloriesBurned).HasColumnName("calories_burned");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.HasOne(e => e.User).WithMany(u => u.Workouts).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // WorkoutExercise configuration
        modelBuilder.Entity<WorkoutExercise>(entity =>
        {
            entity.ToTable("workout_exercises");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.WorkoutId).HasColumnName("workout_id");
            entity.Property(e => e.ExerciseId).HasColumnName("exercise_id");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            entity.HasOne(e => e.Workout).WithMany(w => w.Exercises).HasForeignKey(e => e.WorkoutId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Exercise).WithMany(e => e.WorkoutExercises).HasForeignKey(e => e.ExerciseId).OnDelete(DeleteBehavior.Cascade);
        });

        // ExerciseSet configuration
        modelBuilder.Entity<ExerciseSet>(entity =>
        {
            entity.ToTable("exercise_sets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.WorkoutExerciseId).HasColumnName("workout_exercise_id");
            entity.Property(e => e.SetNumber).HasColumnName("set_number").IsRequired();
            entity.Property(e => e.Reps).HasColumnName("reps");
            entity.Property(e => e.WeightKg).HasColumnName("weight_kg").HasPrecision(6, 2);
            entity.Property(e => e.DurationSeconds).HasColumnName("duration_seconds");
            entity.Property(e => e.DistanceMeters).HasColumnName("distance_meters").HasPrecision(10, 2);
            entity.Property(e => e.Completed).HasColumnName("completed").HasDefaultValue(false);
            entity.HasOne(e => e.WorkoutExercise).WithMany(w => w.Sets).HasForeignKey(e => e.WorkoutExerciseId).OnDelete(DeleteBehavior.Cascade);
        });

        // BodyMeasurement configuration
        modelBuilder.Entity<BodyMeasurement>(entity =>
        {
            entity.ToTable("body_measurements");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.MeasurementDate).HasColumnName("measurement_date").IsRequired();
            entity.Property(e => e.WeightKg).HasColumnName("weight_kg").HasPrecision(6, 2);
            entity.Property(e => e.BodyFatPercentage).HasColumnName("body_fat_percentage").HasPrecision(5, 2);
            entity.Property(e => e.MuscleMassKg).HasColumnName("muscle_mass_kg").HasPrecision(6, 2);
            entity.Property(e => e.WaistCm).HasColumnName("waist_cm").HasPrecision(6, 2);
            entity.Property(e => e.HipsCm).HasColumnName("hips_cm").HasPrecision(6, 2);
            entity.Property(e => e.ChestCm).HasColumnName("chest_cm").HasPrecision(6, 2);
            entity.Property(e => e.ArmsCm).HasColumnName("arms_cm").HasPrecision(6, 2);
            entity.Property(e => e.ThighsCm).HasColumnName("thighs_cm").HasPrecision(6, 2);
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // TrainerClientRelationship configuration
        modelBuilder.Entity<TrainerClientRelationship>(entity =>
        {
            entity.ToTable("trainer_client_relationships");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.TrainerId).HasColumnName("trainer_id");
            entity.Property(e => e.ClientId).HasColumnName("client_id");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("pending");
            entity.Property(e => e.CanViewNutrition).HasColumnName("can_view_nutrition").HasDefaultValue(true);
            entity.Property(e => e.CanViewWorkouts).HasColumnName("can_view_workouts").HasDefaultValue(true);
            entity.Property(e => e.CanViewMeasurements).HasColumnName("can_view_measurements").HasDefaultValue(false);
            entity.Property(e => e.CanMessage).HasColumnName("can_message").HasDefaultValue(true);
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.EndedAt).HasColumnName("ended_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.HasIndex(e => new { e.TrainerId, e.ClientId }).IsUnique();
            entity.HasOne(e => e.Trainer).WithMany(u => u.TrainerRelationships).HasForeignKey(e => e.TrainerId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Client).WithMany(u => u.ClientRelationships).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Cascade);
        });

        // ChatConversation configuration
        modelBuilder.Entity<ChatConversation>(entity =>
        {
            entity.ToTable("chat_conversations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.TrainerClientRelationshipId).HasColumnName("trainer_client_relationship_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            entity.HasOne(e => e.Relationship).WithMany().HasForeignKey(e => e.TrainerClientRelationshipId).OnDelete(DeleteBehavior.Cascade);
        });

        // ChatMessage configuration
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("chat_messages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.SenderId).HasColumnName("sender_id");
            entity.Property(e => e.Content).HasColumnName("content").IsRequired();
            entity.Property(e => e.MessageType).HasColumnName("message_type").HasMaxLength(20).HasDefaultValue("text");
            entity.Property(e => e.SentAt).HasColumnName("sent_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.ReadAt).HasColumnName("read_at");
            entity.HasOne(e => e.Conversation).WithMany(c => c.Messages).HasForeignKey(e => e.ConversationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Sender).WithMany().HasForeignKey(e => e.SenderId).OnDelete(DeleteBehavior.Cascade);
        });

        // Achievement configuration
        modelBuilder.Entity<Achievement>(entity =>
        {
            entity.ToTable("achievements");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IconUrl).HasColumnName("icon_url");
            entity.Property(e => e.Points).HasColumnName("points").HasDefaultValue(0);
            entity.Property(e => e.Category).HasColumnName("category").HasMaxLength(50);
        });

        // UserAchievement configuration
        modelBuilder.Entity<UserAchievement>(entity =>
        {
            entity.ToTable("user_achievements");
            entity.HasKey(e => new { e.UserId, e.AchievementId });
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.AchievementId).HasColumnName("achievement_id");
            entity.Property(e => e.EarnedAt).HasColumnName("earned_at").HasDefaultValueSql("NOW()");
            entity.HasOne(e => e.User).WithMany(u => u.Achievements).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Achievement).WithMany(a => a.UserAchievements).HasForeignKey(e => e.AchievementId).OnDelete(DeleteBehavior.Cascade);
        });

        // Streak configuration
        modelBuilder.Entity<Streak>(entity =>
        {
            entity.ToTable("streaks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.StreakType).HasColumnName("streak_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.CurrentCount).HasColumnName("current_count").HasDefaultValue(0);
            entity.Property(e => e.LongestCount).HasColumnName("longest_count").HasDefaultValue(0);
            entity.Property(e => e.LastActivityDate).HasColumnName("last_activity_date");
            entity.HasOne(e => e.User).WithMany(u => u.Streaks).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // AiChatThread configuration
        modelBuilder.Entity<AiChatThread>(entity =>
        {
            entity.ToTable("ai_chat_threads");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ThreadType).HasColumnName("thread_type").HasMaxLength(50).HasDefaultValue("nutrition");
            entity.Property(e => e.ThreadData).HasColumnName("thread_data").HasColumnType("jsonb").HasDefaultValue("{}");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // AuditLog configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
            entity.Property(e => e.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
            entity.Property(e => e.EntityId).HasColumnName("entity_id").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Details).HasColumnName("details");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            entity.Property(e => e.Timestamp).HasColumnName("timestamp").HasDefaultValueSql("NOW()");
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        // McpToken configuration
        modelBuilder.Entity<McpToken>(entity =>
        {
            entity.ToTable("mcp_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.LastUsedAt).HasColumnName("last_used_at");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.IsActive });
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // McpUsageLog configuration
        modelBuilder.Entity<McpUsageLog>(entity =>
        {
            entity.ToTable("mcp_usage_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.McpTokenId).HasColumnName("mcp_token_id").IsRequired();
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.ToolName).HasColumnName("tool_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Parameters).HasColumnName("parameters").HasColumnType("jsonb");
            entity.Property(e => e.Success).HasColumnName("success").IsRequired();
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(1000);
            entity.Property(e => e.ExecutionTimeMs).HasColumnName("execution_time_ms").IsRequired();
            entity.Property(e => e.Timestamp).HasColumnName("timestamp").HasDefaultValueSql("NOW()");
            entity.HasIndex(e => new { e.UserId, e.Timestamp });
            entity.HasIndex(e => e.McpTokenId);
            entity.HasIndex(e => e.ToolName);
            entity.HasOne(e => e.McpToken).WithMany().HasForeignKey(e => e.McpTokenId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
