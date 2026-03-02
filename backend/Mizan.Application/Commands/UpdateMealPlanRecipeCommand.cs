using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

public record UpdateMealPlanRecipeCommand(
    Guid MealPlanId,
    Guid MealPlanRecipeId,
    DateOnly Date,
    string MealType,
    decimal Servings)
    : IRequest<UpdateMealPlanRecipeResult>;

public record UpdateMealPlanRecipeResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public class UpdateMealPlanRecipeCommandValidator : AbstractValidator<UpdateMealPlanRecipeCommand>
{
    private static readonly string[] ValidMealTypes = ["breakfast", "lunch", "dinner", "snack"];

    public UpdateMealPlanRecipeCommandValidator()
    {
        RuleFor(x => x.MealType)
            .Must(mt => ValidMealTypes.Contains(mt?.ToLowerInvariant()))
            .WithMessage("MealType must be one of: breakfast, lunch, dinner, snack");
        RuleFor(x => x.Servings).GreaterThan(0);
    }
}

public class UpdateMealPlanRecipeCommandHandler : IRequestHandler<UpdateMealPlanRecipeCommand, UpdateMealPlanRecipeResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateMealPlanRecipeCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<UpdateMealPlanRecipeResult> Handle(UpdateMealPlanRecipeCommand request, CancellationToken cancellationToken)
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
            return new UpdateMealPlanRecipeResult
            {
                Success = false,
                Message = "Meal plan not found or access denied"
            };
        }

        if (!await IsAuthorizedAsync(mealPlan, cancellationToken))
        {
            return new UpdateMealPlanRecipeResult
            {
                Success = false,
                Message = "Meal plan not found or access denied"
            };
        }

        var mealPlanRecipe = mealPlan.MealPlanRecipes
            .FirstOrDefault(r => r.Id == request.MealPlanRecipeId);

        if (mealPlanRecipe == null)
        {
            return new UpdateMealPlanRecipeResult
            {
                Success = false,
                Message = "Meal plan recipe not found"
            };
        }

        mealPlanRecipe.Date = request.Date;
        mealPlanRecipe.MealType = request.MealType.ToLowerInvariant();
        mealPlanRecipe.Servings = request.Servings;
        mealPlan.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return new UpdateMealPlanRecipeResult
        {
            Success = true,
            Message = "Meal plan recipe updated successfully"
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
