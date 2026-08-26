using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record ToggleFavoriteRecipeCommand : IRequest<ToggleFavoriteRecipeResult>
{
    public Guid RecipeId { get; init; }
}

public record ToggleFavoriteRecipeResult
{
    public bool IsFavorited { get; init; }
    public string Message { get; init; } = string.Empty;
}

public class ToggleFavoriteRecipeCommandHandler : IRequestHandler<ToggleFavoriteRecipeCommand, ToggleFavoriteRecipeResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly HybridCache _cache;

    public ToggleFavoriteRecipeCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<ToggleFavoriteRecipeResult> Handle(ToggleFavoriteRecipeCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var userId = _currentUser.UserId.Value;

        var existingFavorite = await _context.FavoriteRecipes
            .FirstOrDefaultAsync(f => f.UserId == userId && f.RecipeId == request.RecipeId, cancellationToken);

        bool isFavorited;
        if (existingFavorite != null)
        {
            _context.FavoriteRecipes.Remove(existingFavorite);
            isFavorited = false;
        }
        else
        {
            var favorite = new FavoriteRecipe
            {
                UserId = userId,
                RecipeId = request.RecipeId,
                CreatedAt = DateTime.UtcNow
            };
            _context.FavoriteRecipes.Add(favorite);
            isFavorited = true;
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var state = await _context.FavoriteRecipes
                .AnyAsync(f => f.UserId == userId && f.RecipeId == request.RecipeId, cancellationToken);
            isFavorited = state;
        }

        // IsFavorited rides in the cached recipe DTOs, so a toggle has to
        // clear them even though the recipe itself did not change.
        await _cache.RemoveByTagAsync(CacheTags.Recipes, cancellationToken);

        return new ToggleFavoriteRecipeResult
        {
            IsFavorited = isFavorited,
            Message = isFavorited ? "Added to favorites" : "Removed from favorites"
        };
    }
}
