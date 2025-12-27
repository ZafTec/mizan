using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

public record DeleteRecipeCommand : IRequest<DeleteRecipeResult>
{
    public Guid Id { get; init; }
}

public record DeleteRecipeResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public class DeleteRecipeCommandHandler : IRequestHandler<DeleteRecipeCommand, DeleteRecipeResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteRecipeCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<DeleteRecipeResult> Handle(DeleteRecipeCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return new DeleteRecipeResult { Success = false, Message = "Unauthorized" };
        }

        var recipe = await _context.Recipes
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (recipe == null)
        {
            return new DeleteRecipeResult { Success = false, Message = "Recipe not found" };
        }

        var user = await _context.Users.FindAsync(new object[] { _currentUser.UserId.Value }, cancellationToken);
        var isAdmin = user?.Role == "admin";

        if (recipe.UserId != _currentUser.UserId && !isAdmin)
        {
            return new DeleteRecipeResult { Success = false, Message = "You do not have permission to delete this recipe" };
        }

        var mealPlanUsage = await _context.MealPlanRecipes
            .CountAsync(mpr => mpr.RecipeId == request.Id, cancellationToken);

        if (mealPlanUsage > 0)
        {
            return new DeleteRecipeResult
            {
                Success = false,
                Message = $"Cannot delete recipe. It is currently used in {mealPlanUsage} meal plan(s). Please remove it from all meal plans first or archive it instead."
            };
        }

        _context.Recipes.Remove(recipe);
        await _context.SaveChangesAsync(cancellationToken);

        return new DeleteRecipeResult { Success = true, Message = "Recipe deleted successfully" };
    }
}
