using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Validators;

public class RecipeCircularDependencyValidator
{
    private readonly IMizanDbContext _context;

    public RecipeCircularDependencyValidator(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<bool> WouldCreateCircularDependency(Guid recipeId, Guid subRecipeId)
    {
        var visited = new HashSet<Guid>();
        return await HasCircularDependency(subRecipeId, recipeId, visited);
    }

    private async Task<bool> HasCircularDependency(Guid currentRecipeId, Guid targetRecipeId, HashSet<Guid> visited)
    {
        if (currentRecipeId == targetRecipeId)
            return true;

        if (visited.Contains(currentRecipeId))
            return false;

        visited.Add(currentRecipeId);

        var subRecipeIds = await _context.RecipeIngredients
            .Where(ri => ri.RecipeId == currentRecipeId && ri.SubRecipeId.HasValue)
            .Select(ri => ri.SubRecipeId!.Value)
            .ToListAsync();

        foreach (var subRecipeId in subRecipeIds)
        {
            if (await HasCircularDependency(subRecipeId, targetRecipeId, visited))
                return true;
        }

        return false;
    }
}
