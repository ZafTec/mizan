using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetExercisesQuery : IRequest<GetExercisesResult>, IPagedQuery, ISortableQuery
{
    public string? SearchTerm { get; init; }
    public string? Category { get; init; }
    public string? MuscleGroup { get; init; }
    public string? Equipment { get; init; }
    public bool IncludeCustom { get; init; } = true;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}

public record GetExercisesResult
{
    public List<ExerciseDto> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public List<string> Categories { get; init; } = new();
    public List<string> MuscleGroups { get; init; } = new();
    public List<string> EquipmentOptions { get; init; } = new();
}

public record ExerciseDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Category { get; init; } = string.Empty;
    public string? MuscleGroup { get; init; }
    public string? Equipment { get; init; }
    public string? VideoUrl { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsCustom { get; init; }
    public bool IsOwner { get; init; }
}

public class GetExercisesQueryHandler : IRequestHandler<GetExercisesQuery, GetExercisesResult>
{
    private static readonly Dictionary<string, Expression<Func<Domain.Entities.Exercise, object>>> SortMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = e => e.Name,
        ["category"] = e => e.Category
    };

    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetExercisesQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<GetExercisesResult> Handle(GetExercisesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Exercises.AsQueryable();

        if (_currentUser.UserId.HasValue && request.IncludeCustom)
        {
            query = query.Where(e => !e.IsCustom || e.CreatedByUserId == _currentUser.UserId);
        }
        else
        {
            query = query.Where(e => !e.IsCustom);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.ToLower();
            query = query.Where(e =>
                e.Name.ToLower().Contains(searchTerm) ||
                (e.Description != null && e.Description.ToLower().Contains(searchTerm)));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            query = query.Where(e => e.Category == request.Category);
        }

        if (!string.IsNullOrWhiteSpace(request.MuscleGroup))
        {
            query = query.Where(e => e.MuscleGroup == request.MuscleGroup);
        }

        if (!string.IsNullOrWhiteSpace(request.Equipment))
        {
            query = query.Where(e => e.Equipment == request.Equipment);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var sortedQuery = query.ApplySorting(
            request,
            SortMappings,
            defaultSort: e => e.Name);

        var exercises = await sortedQuery
            .ApplyPaging(request)
            .Select(e => new ExerciseDto
            {
                Id = e.Id,
                Name = e.Name,
                Description = e.Description,
                Category = e.Category,
                MuscleGroup = e.MuscleGroup,
                Equipment = e.Equipment,
                VideoUrl = e.VideoUrl,
                ImageUrl = e.ImageUrl,
                IsCustom = e.IsCustom,
                IsOwner = e.CreatedByUserId == _currentUser.UserId
            })
            .ToListAsync(cancellationToken);

        var allExercises = _context.Exercises.Where(e => !e.IsCustom);
        var categories = await allExercises.Select(e => e.Category).Distinct().OrderBy(c => c).ToListAsync(cancellationToken);
        var muscleGroups = await allExercises.Where(e => e.MuscleGroup != null).Select(e => e.MuscleGroup!).Distinct().OrderBy(m => m).ToListAsync(cancellationToken);
        var equipment = await allExercises.Where(e => e.Equipment != null).Select(e => e.Equipment!).Distinct().OrderBy(eq => eq).ToListAsync(cancellationToken);

        return new GetExercisesResult
        {
            Items = exercises,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            Categories = categories,
            MuscleGroups = muscleGroups,
            EquipmentOptions = equipment
        };
    }
}
