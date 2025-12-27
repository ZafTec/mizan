using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record AddRecipeToMealPlanCommand : IRequest<AddRecipeToMealPlanResult>
{
    public Guid MealPlanId { get; init; }
    public Guid RecipeId { get; init; }
    public DateOnly Date { get; init; }
    public string MealType { get; init; } = "dinner";
    public decimal Servings { get; init; } = 1;
}

public record AddRecipeToMealPlanResult
{
    public Guid Id { get; init; }
    public bool Success { get; init; }
}

public class AddRecipeToMealPlanCommandValidator : AbstractValidator<AddRecipeToMealPlanCommand>
{
    public AddRecipeToMealPlanCommandValidator()
    {
        RuleFor(x => x.MealPlanId).NotEmpty();
        RuleFor(x => x.RecipeId).NotEmpty();
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.MealType).NotEmpty()
            .Must(m => new[] { "breakfast", "lunch", "dinner", "snack" }.Contains(m.ToLower()))
            .WithMessage("Meal type must be breakfast, lunch, dinner, or snack");
        RuleFor(x => x.Servings).GreaterThan(0);
    }
}

public class AddRecipeToMealPlanCommandHandler : IRequestHandler<AddRecipeToMealPlanCommand, AddRecipeToMealPlanResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AddRecipeToMealPlanCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<AddRecipeToMealPlanResult> Handle(AddRecipeToMealPlanCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var mealPlan = await _context.MealPlans
            .FirstOrDefaultAsync(mp => mp.Id == request.MealPlanId, cancellationToken);

        if (mealPlan == null)
        {
            throw new InvalidOperationException("Meal plan not found or access denied");
        }

        // Authorization: User must own the meal plan OR be a member of the household
        if (!await IsAuthorizedAsync(mealPlan, cancellationToken))
        {
            throw new InvalidOperationException("Meal plan not found or access denied");
        }

        var mealPlanRecipe = new MealPlanRecipe
        {
            Id = Guid.NewGuid(),
            MealPlanId = request.MealPlanId,
            RecipeId = request.RecipeId,
            Date = request.Date,
            MealType = request.MealType.ToLower(),
            Servings = request.Servings
        };

        _context.MealPlanRecipes.Add(mealPlanRecipe);
        mealPlan.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return new AddRecipeToMealPlanResult
        {
            Id = mealPlanRecipe.Id,
            Success = true
        };
    }

    private async Task<bool> IsAuthorizedAsync(MealPlan mealPlan, CancellationToken cancellationToken)
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
