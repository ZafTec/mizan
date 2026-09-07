using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

public record PromoteRecipeToPreparationCommand(Guid RecipeId, decimal? YieldGrams = null) : IRequest<Guid>;

public class PromoteRecipeToPreparationCommandValidator : AbstractValidator<PromoteRecipeToPreparationCommand>
{
    public PromoteRecipeToPreparationCommandValidator()
    {
        RuleFor(x => x.RecipeId).NotEmpty();
        RuleFor(x => x.YieldGrams).GreaterThan(0).When(x => x.YieldGrams.HasValue)
            .WithMessage("Yield must be greater than zero grams");
    }
}

public class PromoteRecipeToPreparationCommandHandler : IRequestHandler<PromoteRecipeToPreparationCommand, Guid>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly HybridCache _cache;

    public PromoteRecipeToPreparationCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Guid> Handle(PromoteRecipeToPreparationCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User must be authenticated");
        var recipe = await _context.Recipes.Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId, cancellationToken)
            ?? throw new EntityNotFoundException("Recipe", request.RecipeId);
        if (recipe.UserId != userId)
            throw new ForbiddenAccessException("Recipe does not belong to the current user");
        var yieldGrams = request.YieldGrams ?? recipe.YieldGrams;
        if (yieldGrams is null or <= 0)
            throw new DomainValidationException("This recipe needs a finished weight in grams before it can be used as an ingredient.");

        var food = await RecipePreparation.DeriveAsync(_context, recipe, userId, yieldGrams.Value, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByTagAsync(CacheTags.Foods, cancellationToken);
        await _cache.RemoveByTagAsync(CacheTags.Recipes, cancellationToken);
        return food.Id;
    }
}
