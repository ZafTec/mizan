using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetExercisesQuery : IRequest<GetExercisesResult>
{
    public string? SearchTerm { get; init; }
    public string? Category { get; init; }
    public string? MuscleGroup { get; init; }
    public string? Equipment { get; init; }
    public bool IncludeCustom { get; init; } = true;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public record GetExercisesResult
{
    public List<ExerciseDto> Exercises { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
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

        // Filter to include built-in exercises and optionally user's custom exercises
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

        var exercises = await query
            .OrderBy(e => e.IsCustom)
            .ThenBy(e => e.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
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

        // Get filter options
        var allExercises = _context.Exercises.Where(e => !e.IsCustom);
        var categories = await allExercises.Select(e => e.Category).Distinct().OrderBy(c => c).ToListAsync(cancellationToken);
        var muscleGroups = await allExercises.Where(e => e.MuscleGroup != null).Select(e => e.MuscleGroup!).Distinct().OrderBy(m => m).ToListAsync(cancellationToken);
        var equipment = await allExercises.Where(e => e.Equipment != null).Select(e => e.Equipment!).Distinct().OrderBy(eq => eq).ToListAsync(cancellationToken);

        return new GetExercisesResult
        {
            Exercises = exercises,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            Categories = categories,
            MuscleGroups = muscleGroups,
            EquipmentOptions = equipment
        };
    }
}
