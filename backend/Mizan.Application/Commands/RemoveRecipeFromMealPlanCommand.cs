using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

public record RemoveRecipeFromMealPlanCommand(Guid MealPlanId, Guid MealPlanRecipeId)
    : IRequest<RemoveRecipeFromMealPlanResult>;

public record RemoveRecipeFromMealPlanResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public class RemoveRecipeFromMealPlanCommandHandler : IRequestHandler<RemoveRecipeFromMealPlanCommand, RemoveRecipeFromMealPlanResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RemoveRecipeFromMealPlanCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<RemoveRecipeFromMealPlanResult> Handle(RemoveRecipeFromMealPlanCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var mealPlan = await _context.MealPlans
            .Include(mp => mp.MealPlanRecipes)
            .FirstOrDefaultAsync(mp => mp.Id == request.MealPlanId, cancellationToken);

        if (mealPlan == null)
        {
            return new RemoveRecipeFromMealPlanResult
            {
                Success = false,
                Message = "Meal plan not found or access denied"
            };
        }

        if (!await IsAuthorizedAsync(mealPlan, cancellationToken))
        {
            return new RemoveRecipeFromMealPlanResult
            {
                Success = false,
                Message = "Meal plan not found or access denied"
            };
        }

        var mealPlanRecipe = mealPlan.MealPlanRecipes
            .FirstOrDefault(r => r.Id == request.MealPlanRecipeId);

        if (mealPlanRecipe == null)
        {
            return new RemoveRecipeFromMealPlanResult
            {
                Success = false,
                Message = "Meal plan recipe not found"
            };
        }

        _context.MealPlanRecipes.Remove(mealPlanRecipe);
        mealPlan.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return new RemoveRecipeFromMealPlanResult
        {
            Success = true,
            Message = "Recipe removed from meal plan successfully"
        };
    }

    private async Task<bool> IsAuthorizedAsync(Domain.Entities.MealPlan mealPlan, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue)
        {
            return false;
        }

        if (mealPlan.UserId == userId.Value)
        {
            return true;
        }

        if (mealPlan.HouseholdId.HasValue)
        {
            var isMember = await _context.HouseholdMembers
                .AnyAsync(hm => hm.HouseholdId == mealPlan.HouseholdId.Value && hm.UserId == userId.Value, cancellationToken);
            return isMember;
        }

        return false;
    }
}
