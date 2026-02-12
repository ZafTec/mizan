using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetRecipesQuery : IRequest<PagedResult<RecipeDto>>, IPagedQuery, ISortableQuery
{
    public string? SearchTerm { get; init; }
    public List<string>? Tags { get; init; }
    public bool IncludePublic { get; init; } = true;
    public bool FavoritesOnly { get; init; } = false;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}

public record RecipeDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int Servings { get; init; }
    public int? PrepTimeMinutes { get; init; }
    public int? CookTimeMinutes { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsPublic { get; init; }
    public bool IsOwner { get; init; }
    public RecipeNutritionDto? Nutrition { get; init; }
    public List<string> Tags { get; init; } = new();
    public DateTime CreatedAt { get; init; }
}

public record RecipeNutritionDto
{
    public decimal? CaloriesPerServing { get; init; }
    public decimal? ProteinGrams { get; init; }
    public decimal? CarbsGrams { get; init; }
    public decimal? FatGrams { get; init; }
}

public class GetRecipesQueryHandler : IRequestHandler<GetRecipesQuery, PagedResult<RecipeDto>>
{
    private static readonly Dictionary<string, Expression<Func<Domain.Entities.Recipe, object>>> SortMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["title"] = r => r.Title,
        ["createdat"] = r => r.CreatedAt
    };

    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetRecipesQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<RecipeDto>> Handle(GetRecipesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Recipes
            .Include(r => r.Nutrition)
            .Include(r => r.Tags)
            .AsQueryable();

        if (_currentUser.UserId.HasValue)
        {
            if (request.FavoritesOnly)
            {
                query = from r in query
                        join f in _context.FavoriteRecipes on r.Id equals f.RecipeId
                        where f.UserId == _currentUser.UserId
                        select r;
            }
            else
            {
                query = query.Where(r =>
                    r.UserId == _currentUser.UserId ||
                    (request.IncludePublic && r.IsPublic));
            }
        }
        else
        {
            if (request.FavoritesOnly)
            {
                query = query.Where(r => false);
            }
            else
            {
                query = query.Where(r => r.IsPublic);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.ToLower();
            query = query.Where(r =>
                r.Title.ToLower().Contains(searchTerm) ||
                (r.Description != null && r.Description.ToLower().Contains(searchTerm)));
        }

        if (request.Tags?.Any() == true)
        {
            query = query.Where(r => r.Tags.Any(t => request.Tags.Contains(t.Tag)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var sortedQuery = query.ApplySorting(
            request,
            SortMappings,
            defaultSort: r => r.CreatedAt,
            defaultDescending: true);

        var recipes = await sortedQuery
            .ApplyPaging(request)
            .Select(r => new RecipeDto
            {
                Id = r.Id,
                Title = r.Title,
                Description = r.Description,
                Servings = r.Servings,
                PrepTimeMinutes = r.PrepTimeMinutes,
                CookTimeMinutes = r.CookTimeMinutes,
                ImageUrl = r.ImageUrl,
                IsPublic = r.IsPublic,
                IsOwner = r.UserId == _currentUser.UserId,
                Nutrition = r.Nutrition != null ? new RecipeNutritionDto
                {
                    CaloriesPerServing = r.Nutrition.CaloriesPerServing,
                    ProteinGrams = r.Nutrition.ProteinGrams,
                    CarbsGrams = r.Nutrition.CarbsGrams,
                    FatGrams = r.Nutrition.FatGrams
                } : null,
                Tags = r.Tags.Select(t => t.Tag).ToList(),
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<RecipeDto>
        {
            Items = recipes,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
