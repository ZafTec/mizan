using System.Text.Json;
using MediatR;
using Mizan.Application.Exceptions;
using Mizan.Application.Queries;
using Mizan.Contracts.Workouts;

namespace Mizan.Application.Commands;

/// <summary>What a tool does with the workout it sends.</summary>
public enum NamedWorkoutAction
{
    /// <summary>Log a new workout (<see cref="LogWorkoutCommand"/>).</summary>
    Log,

    /// <summary>Replace a logged workout's details and exercises (<see cref="UpdateWorkoutCommand"/>).</summary>
    Update,

    /// <summary>Save the workout in progress that the web log resumes (<see cref="SaveWorkoutDraftCommand"/>).</summary>
    Draft,
}

/// <summary>
/// A workout from a tool, whose exercises may be named instead of given by id. Each name is
/// resolved to the one exercise the person can see that it means, then the workout is logged,
/// updated or drafted through the command the web uses. Nothing is saved while a name is unknown
/// or ambiguous: the error lists each one with its candidates.
/// </summary>
/// <param name="WorkoutDate">The day; today for a new workout, and the workout's own day for an update.</param>
public sealed record SaveNamedWorkoutCommand(
    NamedWorkoutAction Action,
    Guid? WorkoutId,
    string? Name,
    DateOnly? WorkoutDate,
    int? DurationMinutes,
    decimal? BodyweightKg,
    int? CaloriesBurned,
    string? Notes,
    Guid? TemplateId,
    IReadOnlyList<NamedWorkoutExercise> Exercises) : IRequest<NamedWorkoutResult>;

/// <summary>What was saved: the workout (none for a draft), and the exercise each item was saved against.</summary>
public sealed record NamedWorkoutResult(
    string Action,
    Guid? WorkoutId,
    IReadOnlyList<ResolvedWorkoutExercise> Exercises,
    LogWorkoutResult? Logged);

public sealed class SaveNamedWorkoutCommandHandler : IRequestHandler<SaveNamedWorkoutCommand, NamedWorkoutResult>
{
    private static readonly JsonSerializerOptions DraftJson = new(JsonSerializerDefaults.Web);

    private readonly ISender _sender;

    public SaveNamedWorkoutCommandHandler(ISender sender) => _sender = sender;

    public async Task<NamedWorkoutResult> Handle(SaveNamedWorkoutCommand request, CancellationToken cancellationToken)
    {
        var items = request.Exercises ?? [];
        if (items.Count == 0 && request.Action != NamedWorkoutAction.Draft)
            throw new DomainValidationException("'exercises' needs at least one exercise.");

        var resolved = await ResolveAsync(items, cancellationToken);
        var exercises = items.Select((item, index) => new WorkoutExerciseDto
        {
            ExerciseId = resolved[index].ExerciseId,
            Notes = item.Notes,
            SupersetWithNext = item.SupersetWithNext,
            Sets = item.Sets ?? [],
        }).ToList();

        switch (request.Action)
        {
            case NamedWorkoutAction.Log:
                {
                    var logged = await _sender.Send(new LogWorkoutCommand
                    {
                        Name = request.Name,
                        WorkoutDate = request.WorkoutDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                        DurationMinutes = request.DurationMinutes,
                        BodyweightKg = request.BodyweightKg,
                        CaloriesBurned = request.CaloriesBurned,
                        Notes = request.Notes,
                        TemplateId = request.TemplateId,
                        Exercises = exercises,
                    }, cancellationToken);
                    return new NamedWorkoutResult("logged", logged.Id, resolved, logged);
                }
            case NamedWorkoutAction.Update:
                {
                    var id = request.WorkoutId ?? throw new DomainValidationException("'workoutId' is required to change a workout.");
                    var current = await _sender.Send(new GetWorkoutByIdQuery(id), cancellationToken)
                        ?? throw new EntityNotFoundException("Workout", id);
                    await _sender.Send(new UpdateWorkoutCommand
                    {
                        Id = id,
                        Name = request.Name ?? current.Name,
                        WorkoutDate = request.WorkoutDate ?? current.WorkoutDate,
                        DurationMinutes = request.DurationMinutes ?? current.DurationMinutes,
                        BodyweightKg = request.BodyweightKg ?? current.BodyweightKg,
                        CaloriesBurned = request.CaloriesBurned ?? current.CaloriesBurned,
                        Notes = request.Notes ?? current.Notes,
                        TemplateId = request.TemplateId ?? current.TemplateId,
                        StartedAt = current.StartedAt,
                        CompletedAt = current.CompletedAt,
                        Exercises = exercises,
                    }, cancellationToken);
                    return new NamedWorkoutResult("updated", id, resolved, null);
                }
            default:
                {
                    // The draft's frame is the one the web log writes and SaveWorkoutDraftCommand checks.
                    var payload = JsonSerializer.Serialize(new
                    {
                        name = request.Name,
                        workoutDate = request.WorkoutDate?.ToString("yyyy-MM-dd"),
                        exercises = exercises.Select(e => new { exerciseId = e.ExerciseId, notes = e.Notes, supersetWithNext = e.SupersetWithNext, sets = e.Sets }),
                    }, DraftJson);
                    await _sender.Send(new SaveWorkoutDraftCommand(payload), cancellationToken);
                    return new NamedWorkoutResult("drafted", null, resolved, null);
                }
        }
    }

    private async Task<List<ResolvedWorkoutExercise>> ResolveAsync(IReadOnlyList<NamedWorkoutExercise> items, CancellationToken cancellationToken)
    {
        var resolved = new List<ResolvedWorkoutExercise>();
        var problems = new List<string>();
        var byName = new Dictionary<string, ExerciseDto?>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.ExerciseId is { } id && id != Guid.Empty)
            {
                resolved.Add(new ResolvedWorkoutExercise(index, null, id, null));
                continue;
            }

            var name = item.ExerciseName?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                problems.Add($"exercises[{index}] needs an exerciseId or an exerciseName.");
                resolved.Add(new ResolvedWorkoutExercise(index, null, Guid.Empty, null));
                continue;
            }

            if (!byName.TryGetValue(name, out var match))
            {
                var (found, problem) = await MatchAsync(name, cancellationToken);
                match = found;
                byName[name] = found;
                if (problem is not null) problems.Add($"exercises[{index}] '{name}': {problem}");
            }
            else if (match is null)
            {
                problems.Add($"exercises[{index}] '{name}': as above.");
            }

            resolved.Add(new ResolvedWorkoutExercise(index, name, match?.Id ?? Guid.Empty, match?.Name));
        }

        if (problems.Count > 0) throw new DomainValidationException("Nothing was saved. " + string.Join(" ", problems));
        return resolved;
    }

    /// <summary>
    /// The exercise a name means: the one whose name it is exactly, or the only one it is part of.
    /// Only the catalogue and the person's own exercises count, as the exercise list shows them.
    /// </summary>
    private async Task<(ExerciseDto? Match, string? Problem)> MatchAsync(string name, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetExercisesQuery { SearchTerm = name, Page = 1, PageSize = 20 }, cancellationToken);
        var candidates = result.Items
            .Where(e => !e.IsCustom || e.IsOwner)
            .Where(e => e.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var exact = candidates.Where(e => string.Equals(e.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)).ToList();

        if (exact.Count == 1) return (exact[0], null);
        if (exact.Count == 0 && candidates.Count == 1) return (candidates[0], null);
        if (candidates.Count == 0)
            return (null, "no exercise has that name; find it with get_training view exercises, or add it with save_workout_template kind exercise.");

        var options = (exact.Count > 1 ? exact : candidates).Take(5).Select(e => $"{e.Name} ({e.Id})");
        return (null, $"it could be {string.Join(", ", options)}; send the exerciseId or the full name.");
    }
}
