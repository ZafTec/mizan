using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetProfileExportQuery(Guid UserId) : IRequest<ProfileExportDto?>;

public record ProfileExportDto(
    ProfileExportMetaDto Meta,
    ProfileExportAccountDto Account,
    ProfileExportObservationsDto Observations,
    ProfileExportDataDto Data
);

public record ProfileExportMetaDto(DateTime ExportedAtUtc, string Version);

public record ProfileExportAccountDto(
    Guid Id,
    string Email,
    string? Name,
    string? Image,
    string Role,
    bool EmailVerified,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    ProfileExportAppearanceDto Appearance
);

public record ProfileExportAppearanceDto(
    string Theme,
    bool CompactMode,
    bool ReduceAnimations
);

public record ProfileExportObservationsDto(
    int CurrentStreak,
    ProfileExportGoalSummaryDto? ActiveGoal,
    int TotalMealsLogged,
    int TotalRecipes,
    int TotalMealPlans,
    int TotalMeasurements,
    int TotalWorkouts,
    int TotalAchievements,
    int TotalMcpTokens,
    int TotalMcpRequests,
    DateTime? LastMealLoggedAt,
    DateTime? LastWorkoutLoggedAt,
    DateTime? LastMeasurementLoggedAt
);

public record ProfileExportGoalSummaryDto(
    decimal? TargetCalories,
    decimal? TargetProteinGrams,
    decimal? TargetCarbsGrams,
    decimal? TargetFatGrams,
    decimal? TargetFiberGrams,
    DateOnly? TargetDate
);

public record ProfileExportDataDto(
    List<ProfileExportGoalDto> Goals,
    List<ProfileExportGoalProgressDto> GoalProgressEntries,
    List<ProfileExportMealDto> Meals,
    List<ProfileExportMealPlanDto> MealPlans,
    List<ProfileExportBodyMeasurementDto> BodyMeasurements,
    List<ProfileExportWorkoutDto> Workouts,
    List<ProfileExportAchievementDto> Achievements,
    List<ProfileExportRecipeDto> Recipes,
    List<ProfileExportFavoriteRecipeDto> FavoriteRecipes,
    ProfileExportMcpDto Mcp
);

public record ProfileExportGoalDto(
    Guid Id,
    string? GoalType,
    decimal? TargetCalories,
    decimal? TargetProteinGrams,
    decimal? TargetCarbsGrams,
    decimal? TargetFatGrams,
    decimal? TargetFiberGrams,
    decimal? TargetWeight,
    string? WeightUnit,
    decimal? TargetBodyFatPercentage,
    decimal? TargetMuscleMassKg,
    decimal? TargetProteinCalorieRatio,
    DateOnly? TargetDate,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record ProfileExportGoalProgressDto(
    Guid Id,
    Guid UserGoalId,
    DateOnly Date,
    decimal ActualCalories,
    decimal ActualProteinGrams,
    decimal ActualCarbsGrams,
    decimal ActualFatGrams,
    decimal? ActualWeight,
    string? Notes,
    DateTime CreatedAt
);

public record ProfileExportMealDto(
    Guid Id,
    string Name,
    string MealType,
    DateOnly EntryDate,
    decimal Servings,
    decimal? Calories,
    decimal? ProteinGrams,
    decimal? CarbsGrams,
    decimal? FatGrams,
    decimal? FiberGrams,
    DateTime LoggedAt
);

public record ProfileExportMealPlanDto(
    Guid Id,
    string? Name,
    DateOnly StartDate,
    DateOnly EndDate,
    int RecipeCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record ProfileExportBodyMeasurementDto(
    Guid Id,
    DateOnly MeasurementDate,
    decimal? WeightKg,
    decimal? BodyFatPercentage,
    decimal? MuscleMassKg,
    decimal? WaistCm,
    decimal? HipsCm,
    decimal? ChestCm,
    decimal? LeftArmCm,
    decimal? RightArmCm,
    decimal? LeftThighCm,
    decimal? RightThighCm,
    string? Notes,
    DateTime CreatedAt
);

public record ProfileExportWorkoutDto(
    Guid Id,
    string? Name,
    DateOnly WorkoutDate,
    int? DurationMinutes,
    int? CaloriesBurned,
    string? Notes,
    int ExerciseCount,
    DateTime CreatedAt
);

public record ProfileExportAchievementDto(
    Guid AchievementId,
    string Name,
    string? Description,
    string? Category,
    int Points,
    DateTime EarnedAt
);

public record ProfileExportRecipeDto(
    Guid Id,
    string Title,
    string? Description,
    int Servings,
    int? PrepTimeMinutes,
    int? CookTimeMinutes,
    string? SourceUrl,
    string? ImageUrl,
    bool IsPublic,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record ProfileExportFavoriteRecipeDto(
    Guid RecipeId,
    string RecipeTitle,
    DateTime CreatedAt
);

public record ProfileExportMcpDto(
    List<ProfileExportMcpTokenDto> Tokens,
    List<ProfileExportMcpUsageLogDto> UsageLogs
);

public record ProfileExportMcpTokenDto(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    DateTime? LastUsedAt,
    bool IsActive
);

public record ProfileExportMcpUsageLogDto(
    Guid Id,
    Guid McpTokenId,
    string ToolName,
    bool Success,
    string? ErrorMessage,
    int ExecutionTimeMs,
    DateTime Timestamp
);

public class GetProfileExportQueryHandler : IRequestHandler<GetProfileExportQuery, ProfileExportDto?>
{
    private readonly IMizanDbContext _context;

    public GetProfileExportQueryHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<ProfileExportDto?> Handle(GetProfileExportQuery request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        var goals = await _context.UserGoals
            .AsNoTracking()
            .Where(goal => goal.UserId == request.UserId)
            .OrderByDescending(goal => goal.CreatedAt)
            .Select(goal => new ProfileExportGoalDto(
                goal.Id,
                goal.GoalType,
                goal.TargetCalories,
                goal.TargetProteinGrams,
                goal.TargetCarbsGrams,
                goal.TargetFatGrams,
                goal.TargetFiberGrams,
                goal.TargetWeight,
                goal.WeightUnit,
                goal.TargetBodyFatPercentage,
                goal.TargetMuscleMassKg,
                goal.TargetProteinCalorieRatio,
                goal.TargetDate,
                goal.IsActive,
                goal.CreatedAt,
                goal.UpdatedAt
            ))
            .ToListAsync(cancellationToken);

        var goalProgressEntries = await _context.GoalProgress
            .AsNoTracking()
            .Where(entry => entry.UserId == request.UserId)
            .OrderByDescending(entry => entry.Date)
            .Select(entry => new ProfileExportGoalProgressDto(
                entry.Id,
                entry.UserGoalId,
                entry.Date,
                entry.ActualCalories,
                entry.ActualProteinGrams,
                entry.ActualCarbsGrams,
                entry.ActualFatGrams,
                entry.ActualWeight,
                entry.Notes,
                entry.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        var meals = await _context.FoodDiaryEntries
            .AsNoTracking()
            .Where(entry => entry.UserId == request.UserId)
            .OrderByDescending(entry => entry.EntryDate)
            .ThenByDescending(entry => entry.LoggedAt)
            .Select(entry => new ProfileExportMealDto(
                entry.Id,
                entry.Name,
                entry.MealType,
                entry.EntryDate,
                entry.Servings,
                entry.Calories,
                entry.ProteinGrams,
                entry.CarbsGrams,
                entry.FatGrams,
                entry.FiberGrams,
                entry.LoggedAt
            ))
            .ToListAsync(cancellationToken);

        var mealPlans = await _context.MealPlans
            .AsNoTracking()
            .Where(plan => plan.UserId == request.UserId)
            .Select(plan => new ProfileExportMealPlanDto(
                plan.Id,
                plan.Name,
                plan.StartDate,
                plan.EndDate,
                plan.MealPlanRecipes.Count,
                plan.CreatedAt,
                plan.UpdatedAt
            ))
            .OrderByDescending(plan => plan.CreatedAt)
            .ToListAsync(cancellationToken);

        var bodyMeasurements = await _context.BodyMeasurements
            .AsNoTracking()
            .Where(measurement => measurement.UserId == request.UserId)
            .OrderByDescending(measurement => measurement.MeasurementDate)
            .Select(measurement => new ProfileExportBodyMeasurementDto(
                measurement.Id,
                measurement.MeasurementDate,
                measurement.WeightKg,
                measurement.BodyFatPercentage,
                measurement.MuscleMassKg,
                measurement.WaistCm,
                measurement.HipsCm,
                measurement.ChestCm,
                measurement.LeftArmCm,
                measurement.RightArmCm,
                measurement.LeftThighCm,
                measurement.RightThighCm,
                measurement.Notes,
                measurement.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        var workouts = await _context.Workouts
            .AsNoTracking()
            .Where(workout => workout.UserId == request.UserId)
            .Select(workout => new ProfileExportWorkoutDto(
                workout.Id,
                workout.Name,
                workout.WorkoutDate,
                workout.DurationMinutes,
                workout.CaloriesBurned,
                workout.Notes,
                workout.Exercises.Count,
                workout.CreatedAt
            ))
            .OrderByDescending(workout => workout.WorkoutDate)
            .ToListAsync(cancellationToken);

        var achievements = await _context.UserAchievements
            .AsNoTracking()
            .Where(achievement => achievement.UserId == request.UserId)
            .Select(achievement => new ProfileExportAchievementDto(
                achievement.AchievementId,
                achievement.Achievement.Name,
                achievement.Achievement.Description,
                achievement.Achievement.Category,
                achievement.Achievement.Points,
                achievement.EarnedAt
            ))
            .OrderByDescending(achievement => achievement.EarnedAt)
            .ToListAsync(cancellationToken);

        var recipes = await _context.Recipes
            .AsNoTracking()
            .Where(recipe => recipe.UserId == request.UserId)
            .OrderByDescending(recipe => recipe.CreatedAt)
            .Select(recipe => new ProfileExportRecipeDto(
                recipe.Id,
                recipe.Title,
                recipe.Description,
                recipe.Servings,
                recipe.PrepTimeMinutes,
                recipe.CookTimeMinutes,
                recipe.SourceUrl,
                recipe.ImageUrl,
                recipe.IsPublic,
                recipe.CreatedAt,
                recipe.UpdatedAt
            ))
            .ToListAsync(cancellationToken);

        var favoriteRecipes = await _context.FavoriteRecipes
            .AsNoTracking()
            .Where(recipe => recipe.UserId == request.UserId)
            .OrderByDescending(recipe => recipe.CreatedAt)
            .Select(recipe => new ProfileExportFavoriteRecipeDto(
                recipe.RecipeId,
                recipe.Recipe.Title,
                recipe.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        var mcpTokens = await _context.McpTokens
            .AsNoTracking()
            .Where(token => token.UserId == request.UserId)
            .OrderByDescending(token => token.CreatedAt)
            .Select(token => new ProfileExportMcpTokenDto(
                token.Id,
                token.Name,
                token.CreatedAt,
                token.ExpiresAt,
                token.LastUsedAt,
                token.IsActive
            ))
            .ToListAsync(cancellationToken);

        var mcpUsageLogs = await _context.McpUsageLogs
            .AsNoTracking()
            .Where(log => log.UserId == request.UserId)
            .OrderByDescending(log => log.Timestamp)
            .Select(log => new ProfileExportMcpUsageLogDto(
                log.Id,
                log.McpTokenId,
                log.ToolName,
                log.Success,
                log.ErrorMessage,
                log.ExecutionTimeMs,
                log.Timestamp
            ))
            .ToListAsync(cancellationToken);

        var latestStreak = await _context.Streaks
            .AsNoTracking()
            .Where(streak => streak.UserId == request.UserId)
            .OrderByDescending(streak => streak.LastActivityDate)
            .Select(streak => streak.CurrentCount)
            .FirstOrDefaultAsync(cancellationToken);

        var activeGoal = goals.FirstOrDefault(goal => goal.IsActive);

        var observations = new ProfileExportObservationsDto(
            latestStreak,
            activeGoal == null
                ? null
                : new ProfileExportGoalSummaryDto(
                    activeGoal.TargetCalories,
                    activeGoal.TargetProteinGrams,
                    activeGoal.TargetCarbsGrams,
                    activeGoal.TargetFatGrams,
                    activeGoal.TargetFiberGrams,
                    activeGoal.TargetDate
                ),
            meals.Count,
            recipes.Count,
            mealPlans.Count,
            bodyMeasurements.Count,
            workouts.Count,
            achievements.Count,
            mcpTokens.Count,
            mcpUsageLogs.Count,
            meals.FirstOrDefault()?.LoggedAt,
            workouts.FirstOrDefault()?.CreatedAt,
            bodyMeasurements.FirstOrDefault()?.CreatedAt
        );

        return new ProfileExportDto(
            new ProfileExportMetaDto(DateTime.UtcNow, "1.0"),
            new ProfileExportAccountDto(
                user.Id,
                user.Email,
                user.Name,
                user.Image,
                user.Role,
                user.EmailVerified,
                user.CreatedAt,
                user.UpdatedAt,
                new ProfileExportAppearanceDto(
                    user.ThemePreference,
                    user.CompactMode,
                    user.ReduceAnimations
                )
            ),
            observations,
            new ProfileExportDataDto(
                goals,
                goalProgressEntries,
                meals,
                mealPlans,
                bodyMeasurements,
                workouts,
                achievements,
                recipes,
                favoriteRecipes,
                new ProfileExportMcpDto(mcpTokens, mcpUsageLogs)
            )
        );
    }
}
