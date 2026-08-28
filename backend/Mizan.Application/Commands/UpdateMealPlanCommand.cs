using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

public record UpdateMealPlanCommand(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate)
    : IRequest<UpdateMealPlanResult>;

public record UpdateMealPlanResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public class UpdateMealPlanCommandValidator : AbstractValidator<UpdateMealPlanCommand>
{
    public UpdateMealPlanCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).MaximumLength(255);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
    }
}

public class UpdateMealPlanCommandHandler : IRequestHandler<UpdateMealPlanCommand, UpdateMealPlanResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly HybridCache _cache;

    public UpdateMealPlanCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<UpdateMealPlanResult> Handle(UpdateMealPlanCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var mealPlan = await _context.MealPlans
            .FirstOrDefaultAsync(mp => mp.Id == request.Id, cancellationToken);

        if (mealPlan == null)
        {
            return new UpdateMealPlanResult
            {
                Success = false,
                Message = "Meal plan not found or access denied"
            };
        }

        if (!await IsAuthorizedAsync(mealPlan, cancellationToken))
        {
            return new UpdateMealPlanResult
            {
                Success = false,
                Message = "Meal plan not found or access denied"
            };
        }

        mealPlan.Name = request.Name;
        mealPlan.StartDate = request.StartDate;
        mealPlan.EndDate = request.EndDate;
        mealPlan.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Name/dates changed, so both the owner's list and every viewer's
        // cached detail of this specific plan need to go.
        await _cache.RemoveByTagAsync(CacheTags.MealPlansList(mealPlan.UserId), cancellationToken);
        await _cache.RemoveByTagAsync(CacheTags.MealPlan(mealPlan.Id), cancellationToken);

        return new UpdateMealPlanResult
        {
            Success = true,
            Message = "Meal plan updated successfully"
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
