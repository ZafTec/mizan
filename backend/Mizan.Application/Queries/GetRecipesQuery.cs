using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetRecipesQuery : IRequest<PagedResult<RecipeDto>>, IPagedQuery, ISortableQuery
{
    public string? SearchTerm { get; init; }
    public bool IncludePublic { get; init; } = true;
    public bool FavoritesOnly { get; init; } = false;
    public decimal? MinProteinCalorieRatio { get; init; }
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
    public DateTime CreatedAt { get; init; }
}

public record RecipeNutritionDto
{
    public decimal? CaloriesPerServing { get; init; }
    public decimal? ProteinGrams { get; init; }
    public decimal? CarbsGrams { get; init; }
    public decimal? FatGrams { get; init; }
    public decimal? FiberGrams { get; init; }
    public decimal? ProteinCalorieRatio { get; init; }
}

public class GetRecipesQueryHandler : IRequestHandler<GetRecipesQuery, PagedResult<RecipeDto>>
{
    private static readonly Dictionary<string, Expression<Func<Domain.Entities.Recipe, object>>> SortMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["title"] = r => r.Title,
        ["createdat"] = r => r.CreatedAt
        // "proteinCalorieRatio" is gone: it sorted on the stored recipe_nutrition
        // column, which no longer exists. Nutrition is summed from ingredients on
        // read (docs/REFOCUS.md §4), and sorting a page by a value computed after
        // paging would order only that page - worse than not offering it. Unknown
        // sort keys fall back to the default below.
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
        var query = _context.Recipes.AsQueryable();

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

        // MinProteinCalorieRatio filtered on the stored recipe_nutrition column.
        // Nutrition is now summed on read, and filtering after paging would drop
        // rows from a page rather than from the result - a silently wrong count.
        // Filter by protein density directly from the ingredients instead.
        if (request.MinProteinCalorieRatio.HasValue)
        {
            var minRatio = request.MinProteinCalorieRatio.Value;
            query = query.Where(r =>
                r.Ingredients.Sum(i => i.Food!.CaloriesPer100g * (i.Amount ?? 0m)) > 0
                && r.Ingredients.Sum(i => i.Food!.ProteinPer100g * (i.Amount ?? 0m)) * 400m
                   / r.Ingredients.Sum(i => i.Food!.CaloriesPer100g * (i.Amount ?? 0m)) >= minRatio);
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
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // Nutrition is summed from ingredients for the current page only - two
        // extra queries, rather than a stored column that drifts.
        var totalsById = await RecipeNutritionLookup.ForRecipesAsync(
            _context, recipes.Select(r => r.Id).ToList(), cancellationToken);

        var withNutrition = recipes.Select(r => totalsById.TryGetValue(r.Id, out var t)
            ? r with
            {
                Nutrition = new RecipeNutritionDto
                {
                    CaloriesPerServing = t.Calories,
                    ProteinGrams = t.ProteinGrams,
                    CarbsGrams = t.CarbsGrams,
                    FatGrams = t.FatGrams,
                    FiberGrams = t.FiberGrams,
                    ProteinCalorieRatio = t.ProteinCalorieRatio
                }
            }
            : r).ToList();

        return new PagedResult<RecipeDto>
        {
            Items = withNutrition,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
