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
        var path = new HashSet<Guid> { recipeId };
        return await HasCircularDependency(subRecipeId, path, 0);
    }

    private async Task<bool> HasCircularDependency(Guid currentId, HashSet<Guid> path, int depth)
    {
        if (depth > MaxDependencyDepth)
        {
            throw new InvalidOperationException($"Recipe dependency chain exceeds maximum depth of {MaxDependencyDepth}. This may indicate a circular dependency or overly complex recipe structure.");
        }

        if (path.Contains(currentId))
            return true;

        path.Add(currentId);

        var subRecipeIds = await _context.RecipeIngredients
            .Where(ri => ri.RecipeId == currentId && ri.SubRecipeId.HasValue)
            .Select(ri => ri.SubRecipeId!.Value)
            .ToListAsync();

        foreach (var subId in subRecipeIds)
        {
            if (await HasCircularDependency(subId, path, depth + 1))
                return true;
        }

        path.Remove(currentId);
        return false;
    }
}
