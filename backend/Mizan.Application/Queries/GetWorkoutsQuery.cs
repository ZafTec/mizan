using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Queries;

public record GetWorkoutsQuery : IRequest<PagedResult<WorkoutSummaryDto>>, IPagedQuery, ISortableQuery
{
    public Guid UserId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}

public record WorkoutSummaryDto(
    Guid Id,
    string? Name,
    DateOnly WorkoutDate,
    int? DurationMinutes,
    int? CaloriesBurned,
    string? Notes,
    DateTime CreatedAt,
    List<WorkoutExerciseSummaryDto> Exercises
);

public record WorkoutExerciseSummaryDto(
    Guid Id,
    string ExerciseName,
    string Category,
    string? MuscleGroup,
    int SortOrder,
    List<WorkoutSetDto> Sets
);

public record WorkoutSetDto(
    int SetNumber,
    int? Reps,
    decimal? WeightKg,
    int? DurationSeconds,
    decimal? DistanceMeters,
    bool Completed
);

public class GetWorkoutsQueryHandler : IRequestHandler<GetWorkoutsQuery, PagedResult<WorkoutSummaryDto>>
{
    private static readonly Dictionary<string, Expression<Func<Workout, object>>> SortMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["date"] = w => w.WorkoutDate,
        ["duration"] = w => w.DurationMinutes!,
        ["calories"] = w => w.CaloriesBurned!,
        ["name"] = w => w.Name!,
    };

    private readonly IMizanDbContext _context;

    public GetWorkoutsQueryHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<WorkoutSummaryDto>> Handle(GetWorkoutsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Workouts
            .Where(w => w.UserId == request.UserId);

        var totalCount = await query.CountAsync(cancellationToken);

        var sortedQuery = query.ApplySorting(
            request,
            SortMappings,
            defaultSort: w => w.WorkoutDate,
            defaultDescending: true);

        var workouts = await sortedQuery
            .ApplyPaging(request)
            .Include(w => w.Exercises)
                .ThenInclude(we => we.Exercise)
            .Include(w => w.Exercises)
                .ThenInclude(we => we.Sets)
            .Select(w => new WorkoutSummaryDto(
                w.Id,
                w.Name,
                w.WorkoutDate,
                w.DurationMinutes,
                w.CaloriesBurned,
                w.Notes,
                w.CreatedAt,
                w.Exercises.OrderBy(we => we.SortOrder).Select(we => new WorkoutExerciseSummaryDto(
                    we.Id,
                    we.Exercise.Name,
                    we.Exercise.Category,
                    we.Exercise.MuscleGroup,
                    we.SortOrder,
                    we.Sets.OrderBy(s => s.SetNumber).Select(s => new WorkoutSetDto(
                        s.SetNumber,
                        s.Reps,
                        s.WeightKg,
                        s.DurationSeconds,
                        s.DistanceMeters,
                        s.Completed
                    )).ToList()
                )).ToList()
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<WorkoutSummaryDto>
        {
            Items = workouts,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
