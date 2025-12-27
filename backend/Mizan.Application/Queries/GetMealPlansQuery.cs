using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetMealPlansQuery : IRequest<GetMealPlansResult>
{
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public record GetMealPlansResult
{
    public List<MealPlanDto> MealPlans { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public record MealPlanDto
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public int RecipeCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public class GetMealPlansQueryHandler : IRequestHandler<GetMealPlansQuery, GetMealPlansResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMealPlansQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<GetMealPlansResult> Handle(GetMealPlansQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var query = _context.MealPlans
            .Include(mp => mp.MealPlanRecipes)
            .Where(mp => mp.UserId == _currentUser.UserId);

        if (request.StartDate.HasValue)
        {
            query = query.Where(mp => mp.EndDate >= request.StartDate);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(mp => mp.StartDate <= request.EndDate);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var mealPlans = await query
            .OrderByDescending(mp => mp.StartDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(mp => new MealPlanDto
            {
                Id = mp.Id,
                Name = mp.Name,
                StartDate = mp.StartDate,
                EndDate = mp.EndDate,
                RecipeCount = mp.MealPlanRecipes.Count,
                CreatedAt = mp.CreatedAt,
                UpdatedAt = mp.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new GetMealPlansResult
        {
            MealPlans = mealPlans,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
