using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain;

namespace Mizan.Application.Queries;

public record GetWorkoutByIdQuery(Guid Id) : IRequest<WorkoutSummaryDto?>;
public sealed class GetWorkoutByIdQueryHandler : IRequestHandler<GetWorkoutByIdQuery, WorkoutSummaryDto?>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public GetWorkoutByIdQueryHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public Task<WorkoutSummaryDto?> Handle(GetWorkoutByIdQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        return _context.Workouts.Where(w => w.Id == request.Id && w.UserId == userId).Select(w => new WorkoutSummaryDto(
            w.Id, w.Name, w.WorkoutDate, w.TemplateId, w.BodyweightKg, w.StartedAt, w.CompletedAt, w.DurationMinutes,
            w.CaloriesBurned, w.Notes, w.CreatedAt,
            w.Exercises.OrderBy(e => e.SortOrder).Select(e => new WorkoutExerciseSummaryDto(e.Id, e.ExerciseId, e.Exercise.Name,
                e.Exercise.Category, e.Exercise.MuscleGroup, e.SortOrder, e.Notes, e.SupersetWithNext,
                e.Sets.OrderBy(s => s.SetNumber).Select(s => new WorkoutSetDto(s.SetNumber, s.Reps, s.WeightKg, s.DurationSeconds,
                    s.DistanceMeters, s.ResistanceLevel, s.InclinePercent, s.Steps, s.CompletedAt, s.Completed)).ToList())).ToList())).FirstOrDefaultAsync(ct);
    }
}

public record WorkoutDraftDto(string Payload, DateTime UpdatedAt);
public record GetWorkoutDraftQuery : IRequest<WorkoutDraftDto?>;
public sealed class GetWorkoutDraftQueryHandler : IRequestHandler<GetWorkoutDraftQuery, WorkoutDraftDto?>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public GetWorkoutDraftQueryHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public Task<WorkoutDraftDto?> Handle(GetWorkoutDraftQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        return _context.WorkoutDrafts.Where(d => d.UserId == userId).Select(d => new WorkoutDraftDto(d.Payload, d.UpdatedAt)).FirstOrDefaultAsync(ct);
    }
}

public record WorkoutTemplateExerciseDto(Guid Id, Guid ExerciseId, string ExerciseName, string Category, string? MuscleGroup,
    int SortOrder, int Sets, int? RepsPerSet, decimal? TargetWeightKg, int? RestSecondsMin, int? RestSecondsMax,
    int? RestSecondsFailure, bool SupersetWithNext, string? Notes, string ProgressionType, string ProgressionStrategy,
    decimal? ProgressionAmountKg, string TargetType, int? TargetSeconds, decimal? TargetDistanceMeters);
public record WorkoutTemplateDto(Guid Id, Guid? UserId, string Name, string? ProgramName, int SessionOrder, string? Notes,
    bool IsBuiltIn, int SortOrder, IReadOnlyList<WorkoutTemplateExerciseDto> Exercises);
public record GetWorkoutTemplatesQuery : IRequest<IReadOnlyList<WorkoutTemplateDto>>;
public sealed class GetWorkoutTemplatesQueryHandler : IRequestHandler<GetWorkoutTemplatesQuery, IReadOnlyList<WorkoutTemplateDto>>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public GetWorkoutTemplatesQueryHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task<IReadOnlyList<WorkoutTemplateDto>> Handle(GetWorkoutTemplatesQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        return await _context.WorkoutTemplates.Where(t => t.IsBuiltIn || t.UserId == userId).OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .Select(t => new WorkoutTemplateDto(t.Id, t.UserId, t.Name, t.ProgramName, t.SessionOrder, t.Notes, t.IsBuiltIn, t.SortOrder,
                t.Exercises.OrderBy(e => e.SortOrder).Select(e => new WorkoutTemplateExerciseDto(e.Id, e.ExerciseId, e.Exercise.Name,
                    e.Exercise.Category, e.Exercise.MuscleGroup, e.SortOrder, e.Sets, e.RepsPerSet, e.TargetWeightKg,
                    e.RestSecondsMin, e.RestSecondsMax, e.RestSecondsFailure, e.SupersetWithNext, e.Notes,
                    e.ProgressionType, e.ProgressionStrategy, e.ProgressionAmountKg, e.TargetType, e.TargetSeconds,
                    e.TargetDistanceMeters)).ToList())).ToListAsync(ct);
    }
}

public record PlannedSetDto(int SetNumber, int? TargetReps, decimal WeightKg, int? TargetSeconds, decimal? TargetDistanceMeters);
public record NextSessionExerciseDto(Guid ExerciseId, string Name, string Category, string? Notes, bool SupersetWithNext,
    int? RestSecondsMin, int? RestSecondsMax, int? RestSecondsFailure, IReadOnlyList<PlannedSetDto> Sets);
public record NextSessionDto(Guid TemplateId, string Name, string? ProgramName, IReadOnlyList<NextSessionExerciseDto> Exercises);
public record GetNextTemplateSessionQuery(Guid Id) : IRequest<NextSessionDto?>;
public sealed class GetNextTemplateSessionQueryHandler : IRequestHandler<GetNextTemplateSessionQuery, NextSessionDto?>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public GetNextTemplateSessionQueryHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task<NextSessionDto?> Handle(GetNextTemplateSessionQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var template = await _context.WorkoutTemplates.AsNoTracking().Include(t => t.Exercises).ThenInclude(e => e.Exercise)
            .FirstOrDefaultAsync(t => t.Id == request.Id && (t.IsBuiltIn || t.UserId == userId), ct);
        if (template is null) return null;
        var last = await _context.Workouts.AsNoTracking().Include(w => w.Exercises).ThenInclude(e => e.Sets)
            .Where(w => w.UserId == userId && w.TemplateId == template.Id).OrderByDescending(w => w.WorkoutDate).ThenByDescending(w => w.CreatedAt).FirstOrDefaultAsync(ct);
        var result = new List<NextSessionExerciseDto>();
        foreach (var exercise in template.Exercises.OrderBy(e => e.SortOrder))
        {
            var previous = last?.Exercises.FirstOrDefault(e => e.ExerciseId == exercise.ExerciseId);
            var weights = previous?.Sets.OrderBy(s => s.SetNumber).Select(s => s.WeightKg ?? exercise.TargetWeightKg ?? 0).ToArray()
                ?? Enumerable.Repeat(exercise.TargetWeightKg ?? 0, exercise.Sets).ToArray();
            if (weights.Length != exercise.Sets) weights = Enumerable.Range(0, exercise.Sets).Select(i => i < weights.Length ? weights[i] : weights.LastOrDefault()).ToArray();
            var succeeded = previous is not null && previous.Sets.Count == exercise.Sets && previous.Sets.All(s => s.Completed && (!exercise.RepsPerSet.HasValue || s.Reps >= exercise.RepsPerSet));
            var progressed = succeeded ? WorkoutProgression.Apply(weights, exercise.ProgressionType, exercise.ProgressionAmountKg ?? 0, exercise.ProgressionStrategy) : weights;
            result.Add(new NextSessionExerciseDto(exercise.ExerciseId, exercise.Exercise.Name, exercise.Exercise.Category, exercise.Notes,
                exercise.SupersetWithNext, exercise.RestSecondsMin, exercise.RestSecondsMax, exercise.RestSecondsFailure,
                progressed.Select((weight, index) => new PlannedSetDto(index + 1, exercise.RepsPerSet, weight, exercise.TargetSeconds, exercise.TargetDistanceMeters)).ToList()));
        }
        return new NextSessionDto(template.Id, template.Name, template.ProgramName, result);
    }
}

public record ExercisePoint(DateOnly Date, decimal TopWeightKg, decimal EstimatedOneRepMax, bool IsPersonalRecord);
public record ExerciseStatsDto(Guid ExerciseId, string Name, IReadOnlyList<ExercisePoint> Points);
public record HeaviestLiftDto(Guid ExerciseId, string Name, decimal WeightKg, DateOnly Date);
public record MuscleGroupSetsDto(string MuscleGroup, int Sets);
public record WorkoutStatsDto(decimal WorkoutsPerWeek, decimal SetsPerWeek, decimal AverageSessionMinutes, decimal TotalVolumeKg,
    int PersonalRecordCount, HeaviestLiftDto? HeaviestLift, decimal MaxSessionVolume, IReadOnlyList<ExerciseStatsDto> PerExercise,
    IReadOnlyList<MuscleGroupSetsDto> PerMuscleGroup);
public record GetWorkoutStatsQuery(DateOnly? From = null, DateOnly? To = null) : IRequest<WorkoutStatsDto>;
public sealed class GetWorkoutStatsQueryHandler : IRequestHandler<GetWorkoutStatsQuery, WorkoutStatsDto>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public GetWorkoutStatsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task<WorkoutStatsDto> Handle(GetWorkoutStatsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var to = request.To ?? DateOnly.FromDateTime(DateTime.UtcNow); var from = request.From ?? to.AddDays(-89);
        var workouts = await _context.Workouts.AsNoTracking().Where(w => w.UserId == userId && w.WorkoutDate >= from && w.WorkoutDate <= to)
            .Include(w => w.Exercises).ThenInclude(e => e.Exercise).Include(w => w.Exercises).ThenInclude(e => e.Sets).ToListAsync(ct);
        var days = Math.Max(1, to.DayNumber - from.DayNumber + 1); var weeks = days / 7m;
        var allSets = workouts.SelectMany(w => w.Exercises.SelectMany(e => e.Sets.Select(s => (w, e, s)))).Where(x => x.s.Completed).ToList();
        var volume = allSets.Sum(x => (x.s.WeightKg ?? 0) * (x.s.Reps ?? 0));
        var heaviest = allSets.Where(x => x.s.WeightKg.HasValue).OrderByDescending(x => x.s.WeightKg).Select(x => new HeaviestLiftDto(x.e.ExerciseId, x.e.Exercise.Name, x.s.WeightKg!.Value, x.w.WorkoutDate)).FirstOrDefault();
        var personalRecordCount = 0;
        var perExercise = allSets.GroupBy(x => new { x.e.ExerciseId, x.e.Exercise.Name }).Select(group =>
        {
            decimal? priorBest = null;
            var points = group
                .GroupBy(x => new { x.w.Id, x.w.WorkoutDate, x.w.CreatedAt })
                .OrderBy(workout => workout.Key.WorkoutDate)
                .ThenBy(workout => workout.Key.CreatedAt)
                .ThenBy(workout => workout.Key.Id)
                .Select(workout =>
            {
                var top = workout.Max(x => x.s.WeightKg ?? 0);
                var rep = workout.OrderByDescending(x => x.s.WeightKg).First().s.Reps ?? 0;
                var isPersonalRecord = top > 0 && (!priorBest.HasValue || top > priorBest.Value);
                if (isPersonalRecord)
                {
                    priorBest = top;
                    personalRecordCount++;
                }
                return new ExercisePoint(workout.Key.WorkoutDate, top, top * (1 + rep / 30m), isPersonalRecord);
            }).ToList();
            return new ExerciseStatsDto(group.Key.ExerciseId, group.Key.Name, points);
        }).ToList();
        var muscle = allSets.Where(x => !string.IsNullOrWhiteSpace(x.e.Exercise.MuscleGroup)).GroupBy(x => x.e.Exercise.MuscleGroup!).Select(g => new MuscleGroupSetsDto(g.Key, g.Count())).ToList();
        var maxSession = workouts.Select(w => w.Exercises.SelectMany(e => e.Sets).Sum(s => (s.WeightKg ?? 0) * (s.Reps ?? 0))).DefaultIfEmpty(0).Max();
        return new WorkoutStatsDto(workouts.Count / weeks, allSets.Count / weeks, workouts.Count == 0 ? 0 : (decimal)workouts.Average(w => w.DurationMinutes ?? 0), volume, personalRecordCount, heaviest, maxSession, perExercise, muscle);
    }
}

public record GetClientWorkoutsQuery(Guid ClientId, int Page = 1, int PageSize = 20) : IRequest<Common.PagedResult<WorkoutSummaryDto>>;
public sealed class GetClientWorkoutsQueryHandler : IRequestHandler<GetClientWorkoutsQuery, Common.PagedResult<WorkoutSummaryDto>>
{
    private readonly ITrainerAuthorizationService _authorization; private readonly IMediator _mediator;
    public GetClientWorkoutsQueryHandler(ITrainerAuthorizationService authorization, IMediator mediator) { _authorization = authorization; _mediator = mediator; }
    public async Task<Common.PagedResult<WorkoutSummaryDto>> Handle(GetClientWorkoutsQuery request, CancellationToken ct)
    {
        var relationship = await _authorization.GetRelationshipForCurrentTrainerAndClientAsync(request.ClientId, true, ct);
        if (!relationship.CanViewWorkouts) throw new UnauthorizedAccessException("Workout access is disabled");
        return await _mediator.Send(new GetWorkoutsQuery { UserId = request.ClientId, Page = request.Page, PageSize = request.PageSize }, ct);
    }
}
