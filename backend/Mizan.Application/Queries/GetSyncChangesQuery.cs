using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Queries;

/// <summary>
/// What changed for the signed-in user since a moment, in one call. An app that keeps a copy of the
/// diary, workouts, measurements and notifications asks this on launch and whenever it regains signal,
/// and applies the answer: upsert what is listed, remove what is in <c>Deleted</c>.
/// </summary>
public record GetSyncChangesQuery(DateTime? Since, int Limit = 200) : IRequest<SyncChangesResult>;

public record SyncDiaryEntryDto
{
    public Guid Id { get; init; }
    public DateOnly EntryDate { get; init; }
    public Guid? FoodId { get; init; }
    public Guid? RecipeId { get; init; }
    public Guid? GroupId { get; init; }
    public string? GroupName { get; init; }
    public decimal? AmountGrams { get; init; }
    public string MealType { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal Servings { get; init; }
    public decimal? Calories { get; init; }
    public decimal? ProteinGrams { get; init; }
    public decimal? CarbsGrams { get; init; }
    public decimal? FatGrams { get; init; }
    public decimal? FiberGrams { get; init; }
    public decimal? ProteinCalorieRatio { get; init; }
    public DateTime LoggedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record SyncDeletionDto(string Type, Guid Id, DateTime DeletedAt);

public record SyncChangesResult
{
    /// <summary>When the server answered. Informational; <see cref="NextSince"/> is what to send next.</summary>
    public DateTime ServerTime { get; init; }

    /// <summary>
    /// True when the app was away longer than deletions are remembered, so it cannot trust its copy.
    /// It should drop what it holds and sync again with no <c>since</c>.
    /// </summary>
    public bool ResyncRequired { get; init; }

    /// <summary>More changes wait. Ask again with <see cref="NextSince"/> straight away.</summary>
    public bool HasMore { get; init; }

    /// <summary>Send this as <c>since</c> next time. It always moves forward, and re-delivery is harmless because every item is an upsert.</summary>
    public DateTime NextSince { get; init; }

    public List<SyncDiaryEntryDto> DiaryEntries { get; init; } = new();
    public List<WorkoutSummaryDto> Workouts { get; init; } = new();
    public List<BodyMeasurementDto> BodyMeasurements { get; init; } = new();
    public List<NotificationDto> Notifications { get; init; } = new();
    public List<SyncDeletionDto> Deleted { get; init; } = new();
}

public sealed class GetSyncChangesQueryHandler : IRequestHandler<GetSyncChangesQuery, SyncChangesResult>
{
    /// <summary>How long a deletion is remembered. An app away longer than this must start over.</summary>
    public static readonly TimeSpan TombstoneRetention = TimeSpan.FromDays(90);

    /// <summary>
    /// Subtracted from the final cursor so a write that was still committing while this ran is not skipped
    /// by the next call. A few items arrive twice; that is harmless.
    /// </summary>
    private static readonly TimeSpan Overlap = TimeSpan.FromSeconds(5);

    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetSyncChangesQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<SyncChangesResult> Handle(GetSyncChangesQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var now = DateTime.UtcNow;
        var limit = Math.Clamp(request.Limit, 1, 500);
        var since = request.Since?.ToUniversalTime();

        if (since is not null && now - since > TombstoneRetention)
            return new SyncChangesResult { ServerTime = now, ResyncRequired = true, NextSince = now };

        var floor = since ?? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Opportunistic: the tombstones that no app could still need go as people sync.
        await _context.DeletedRecords.Where(d => d.UserId == userId && d.DeletedAt < now - TombstoneRetention).ExecuteDeleteAsync(ct);

        var diary = await Page(
            _context.FoodDiaryEntries.Include(e => e.Food).Include(e => e.Recipe).Where(e => e.UserId == userId),
            e => e.UpdatedAt, floor, limit, ct);
        var workouts = await Page(
            _context.Workouts.Include(w => w.Exercises).ThenInclude(x => x.Exercise)
                .Include(w => w.Exercises).ThenInclude(x => x.Sets).Where(w => w.UserId == userId),
            w => w.UpdatedAt, floor, limit, ct);
        var measurements = await Page(
            _context.BodyMeasurements.Where(m => m.UserId == userId), m => m.UpdatedAt, floor, limit, ct);
        var notifications = await Page(
            _context.Notifications.Where(n => n.UserId == userId), n => n.ReadAt ?? n.CreatedAt, floor, limit, ct);
        var deleted = await Page(
            _context.DeletedRecords.Where(d => d.UserId == userId), d => d.DeletedAt, floor, limit, ct);

        // The cursor stops at the earliest point any list was cut, so nothing past a cut is skipped.
        var cuts = new[] { diary.CutAt, workouts.CutAt, measurements.CutAt, notifications.CutAt, deleted.CutAt }
            .Where(c => c is not null).Select(c => c!.Value).ToList();
        var hasMore = cuts.Count > 0;
        var next = hasMore ? cuts.Min() + Microsecond : now - Overlap;
        if (next < floor) next = floor;

        return new SyncChangesResult
        {
            ServerTime = now,
            HasMore = hasMore,
            NextSince = next,
            DiaryEntries = diary.Items.Select(ToDto).ToList(),
            Workouts = workouts.Items.Select(ToDto).ToList(),
            BodyMeasurements = measurements.Items.Select(m => new BodyMeasurementDto(
                m.Id, m.MeasurementDate.ToDateTime(TimeOnly.MinValue), m.WeightKg, m.BodyFatPercentage, m.MuscleMassKg, m.WaistCm,
                m.HipsCm, m.ChestCm, m.LeftArmCm, m.RightArmCm, m.LeftThighCm, m.RightThighCm, m.Notes)).ToList(),
            Notifications = notifications.Items.Select(n => new NotificationDto(
                n.Id, n.Type, n.Title, n.Body, n.LinkUrl, n.CreatedAt, n.ReadAt)).ToList(),
            Deleted = deleted.Items.Select(d => new SyncDeletionDto(d.EntityType, d.EntityId, d.DeletedAt)).ToList(),
        };
    }

    private sealed record PageResult<T>(List<T> Items, DateTime? CutAt);

    /// <summary>
    /// The oldest changes first, up to the limit. A page never ends inside a run of rows that share a
    /// timestamp (one save stamps them all alike), so the cursor can move strictly forward.
    /// </summary>
    private static async Task<PageResult<T>> Page<T>(
        IQueryable<T> source, Expression<Func<T, DateTime>> stamp, DateTime floor, int limit, CancellationToken ct)
        where T : class
    {
        var parameter = stamp.Parameters[0];
        Expression<Func<T, bool>> After(DateTime value, bool strict) => Expression.Lambda<Func<T, bool>>(
            strict
                ? Expression.GreaterThan(stamp.Body, Expression.Constant(value))
                : Expression.GreaterThanOrEqual(stamp.Body, Expression.Constant(value)),
            parameter);

        var items = await source.Where(After(floor, strict: false)).OrderBy(stamp).Take(limit + 1).ToListAsync(ct);
        if (items.Count <= limit) return new PageResult<T>(items, null);

        items.RemoveAt(items.Count - 1);
        var last = stamp.Compile()(items[^1]);

        // Whatever else shares the last timestamp comes along, so the next page starts after all of it.
        var tied = await source.Where(Expression.Lambda<Func<T, bool>>(Expression.Equal(stamp.Body, Expression.Constant(last)), parameter)).ToListAsync(ct);
        foreach (var row in tied.Where(r => !items.Contains(r))) items.Add(row);

        return new PageResult<T>(items, last);
    }

    /// <summary>The smallest step the database can tell apart, so the next page starts after everything delivered here.</summary>
    private static readonly TimeSpan Microsecond = TimeSpan.FromTicks(10);

    private static SyncDiaryEntryDto ToDto(FoodDiaryEntry e) => new()
    {
        Id = e.Id, EntryDate = e.EntryDate, FoodId = e.FoodId, RecipeId = e.RecipeId, GroupId = e.GroupId, GroupName = e.GroupName,
        AmountGrams = e.AmountGrams, MealType = e.MealType,
        Name = !string.IsNullOrEmpty(e.Name) ? e.Name : (e.Food?.Name ?? e.Recipe?.Title ?? "Unknown"),
        Servings = e.Servings, Calories = e.Calories, ProteinGrams = e.ProteinGrams, CarbsGrams = e.CarbsGrams, FatGrams = e.FatGrams,
        FiberGrams = e.FiberGrams, ProteinCalorieRatio = e.ProteinCalorieRatio, LoggedAt = e.LoggedAt, UpdatedAt = e.UpdatedAt,
    };

    private static WorkoutSummaryDto ToDto(Workout w) => new(
        w.Id, w.Name, w.WorkoutDate, w.TemplateId, w.BodyweightKg, w.StartedAt, w.CompletedAt, w.DurationMinutes, w.CaloriesBurned, w.Notes, w.CreatedAt,
        w.Exercises.OrderBy(x => x.SortOrder).Select(x => new WorkoutExerciseSummaryDto(
            x.Id, x.ExerciseId, x.Exercise.Name, x.Exercise.Category, x.Exercise.MuscleGroup, x.SortOrder, x.Notes, x.SupersetWithNext,
            x.Sets.OrderBy(s => s.SetNumber).Select(s => new WorkoutSetDto(
                s.SetNumber, s.Reps, s.WeightKg, s.DurationSeconds, s.DistanceMeters, s.ResistanceLevel, s.InclinePercent, s.Steps, s.CompletedAt, s.Completed)).ToList()
        )).ToList());
}
