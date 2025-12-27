using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

public record DeleteMealPlanCommand(Guid Id) : IRequest<DeleteMealPlanResult>;

public record DeleteMealPlanResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public class DeleteMealPlanCommandHandler : IRequestHandler<DeleteMealPlanCommand, DeleteMealPlanResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteMealPlanCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<DeleteMealPlanResult> Handle(DeleteMealPlanCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var mealPlan = await _context.MealPlans
            .Include(mp => mp.MealPlanRecipes)
            .FirstOrDefaultAsync(mp => mp.Id == request.Id, cancellationToken);

        if (mealPlan == null)
        {
            return new DeleteMealPlanResult
            {
                Success = false,
                Message = "Meal plan not found or access denied"
            };
        }

        // Authorization: User must own the meal plan OR be a member of the household
        if (!await IsAuthorizedAsync(mealPlan, cancellationToken))
        {
            return new DeleteMealPlanResult
            {
                Success = false,
                Message = "Meal plan not found or access denied"
            };
        }

        _context.MealPlanRecipes.RemoveRange(mealPlan.MealPlanRecipes);
        _context.MealPlans.Remove(mealPlan);
        await _context.SaveChangesAsync(cancellationToken);

        return new DeleteMealPlanResult
        {
            Success = true,
            Message = "Meal plan deleted successfully"
        };
    }

    private async Task<bool> IsAuthorizedAsync(Domain.Entities.MealPlan mealPlan, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue)
        {
            return false;
        }

        // User owns the meal plan
        if (mealPlan.UserId == userId.Value)
        {
            return true;
        }

        // Meal plan belongs to a household and user is a member
        if (mealPlan.HouseholdId.HasValue)
        {
            var isMember = await _context.HouseholdMembers
                .AnyAsync(hm => hm.HouseholdId == mealPlan.HouseholdId.Value && hm.UserId == userId.Value, cancellationToken);
            return isMember;
        }

        return false;
    }
}
