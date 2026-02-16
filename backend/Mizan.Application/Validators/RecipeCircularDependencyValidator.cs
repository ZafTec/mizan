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

    private const int MaxDependencyDepth = 20;

    public async Task<bool> WouldCreateCircularDependency(Guid recipeId, Guid subRecipeId)
    {
        var visited = new HashSet<Guid>();
        return await HasCircularDependency(subRecipeId, recipeId, visited, 0);
    }

    private async Task<bool> HasCircularDependency(Guid currentRecipeId, Guid targetRecipeId, HashSet<Guid> visited, int depth)
    {
        if (depth > MaxDependencyDepth)
        {
            throw new InvalidOperationException($"Recipe dependency chain exceeds maximum depth of {MaxDependencyDepth}. This may indicate a circular dependency or overly complex recipe structure.");
        }

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
            if (await HasCircularDependency(subRecipeId, targetRecipeId, visited, depth + 1))
                return true;
        }

        return false;
    }
}
