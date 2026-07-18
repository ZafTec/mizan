using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record DeleteWorkoutCommand(Guid Id) : IRequest;
public sealed class DeleteWorkoutCommandHandler : IRequestHandler<DeleteWorkoutCommand>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    public DeleteWorkoutCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task Handle(DeleteWorkoutCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var workout = await _context.Workouts.FirstOrDefaultAsync(w => w.Id == request.Id && w.UserId == userId, ct)
            ?? throw new EntityNotFoundException("Workout not found");
        _context.Workouts.Remove(workout);
        await _context.SaveChangesAsync(ct);
    }
}

public record UpdateWorkoutCommand : IRequest
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public DateOnly WorkoutDate { get; init; }
    public Guid? TemplateId { get; init; }
    public decimal? BodyweightKg { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int? DurationMinutes { get; init; }
    public int? CaloriesBurned { get; init; }
    public string? Notes { get; init; }
    public List<WorkoutExerciseDto> Exercises { get; init; } = [];
}

public sealed class UpdateWorkoutCommandValidator : AbstractValidator<UpdateWorkoutCommand>
{
    public UpdateWorkoutCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.Exercises).NotEmpty().Must(x => x.Count <= 30);
    }
}

public sealed class UpdateWorkoutCommandHandler : IRequestHandler<UpdateWorkoutCommand>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    public UpdateWorkoutCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task Handle(UpdateWorkoutCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var workout = await _context.Workouts.Include(w => w.Exercises).ThenInclude(e => e.Sets)
            .FirstOrDefaultAsync(w => w.Id == request.Id && w.UserId == userId, ct)
            ?? throw new EntityNotFoundException("Workout not found");
        var ids = request.Exercises.Select(e => e.ExerciseId).Distinct().ToArray();
        var valid = await _context.Exercises.CountAsync(e => ids.Contains(e.Id) && (!e.IsCustom || e.CreatedByUserId == userId), ct);
        if (valid != ids.Length) throw new DomainValidationException("One or more exercises are invalid");

        _context.WorkoutExercises.RemoveRange(workout.Exercises);
        workout.Name = request.Name;
        workout.WorkoutDate = request.WorkoutDate;
        workout.TemplateId = request.TemplateId;
        workout.BodyweightKg = request.BodyweightKg;
        workout.StartedAt = request.StartedAt;
        workout.CompletedAt = request.CompletedAt;
        workout.DurationMinutes = request.DurationMinutes;
        workout.CaloriesBurned = request.CaloriesBurned;
        workout.Notes = request.Notes;
        workout.Exercises = request.Exercises.Select((exercise, index) => new WorkoutExercise
        {
            Id = Guid.NewGuid(),
            WorkoutId = workout.Id,
            ExerciseId = exercise.ExerciseId,
            SortOrder = index,
            Notes = exercise.Notes,
            SupersetWithNext = exercise.SupersetWithNext,
            Sets = exercise.Sets.Select((set, setIndex) => new ExerciseSet
            {
                Id = Guid.NewGuid(),
                SetNumber = setIndex + 1,
                Reps = set.Reps,
                WeightKg = set.WeightKg,
                DurationSeconds = set.DurationSeconds,
                DistanceMeters = set.DistanceMeters,
                ResistanceLevel = set.ResistanceLevel,
                InclinePercent = set.InclinePercent,
                Steps = set.Steps,
                Completed = set.Completed,
                CompletedAt = set.CompletedAt
            }).ToList()
        }).ToList();
        await _context.SaveChangesAsync(ct);
    }
}

public record SaveWorkoutDraftCommand(string Payload) : IRequest;
public sealed class SaveWorkoutDraftCommandValidator : AbstractValidator<SaveWorkoutDraftCommand>
{
    public SaveWorkoutDraftCommandValidator() => RuleFor(x => x.Payload).NotEmpty().MaximumLength(1_000_000).Must(BeJson).WithMessage("Payload must be valid JSON");
    private static bool BeJson(string value) { try { System.Text.Json.JsonDocument.Parse(value); return true; } catch { return false; } }
}
public sealed class SaveWorkoutDraftCommandHandler : IRequestHandler<SaveWorkoutDraftCommand>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public SaveWorkoutDraftCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task Handle(SaveWorkoutDraftCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var draft = await _context.WorkoutDrafts.FirstOrDefaultAsync(d => d.UserId == userId, ct);
        if (draft is null) _context.WorkoutDrafts.Add(new WorkoutDraft { UserId = userId, Payload = request.Payload, UpdatedAt = DateTime.UtcNow });
        else { draft.Payload = request.Payload; draft.UpdatedAt = DateTime.UtcNow; }
        await _context.SaveChangesAsync(ct);
    }
}

public record DeleteWorkoutDraftCommand : IRequest;
public sealed class DeleteWorkoutDraftCommandHandler : IRequestHandler<DeleteWorkoutDraftCommand>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public DeleteWorkoutDraftCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task Handle(DeleteWorkoutDraftCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        await _context.WorkoutDrafts.Where(d => d.UserId == userId).ExecuteDeleteAsync(ct);
    }
}

public record WorkoutTemplateExerciseInput(
    Guid ExerciseId, int SortOrder, int Sets, int? RepsPerSet, decimal? TargetWeightKg,
    int? RestSecondsMin, int? RestSecondsMax, int? RestSecondsFailure, bool SupersetWithNext,
    string? Notes, string ProgressionType, string ProgressionStrategy, decimal? ProgressionAmountKg,
    string TargetType, int? TargetSeconds, decimal? TargetDistanceMeters);

public record SaveWorkoutTemplateCommand : IRequest<Guid>
{
    public Guid? Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ProgramName { get; init; }
    public int SessionOrder { get; init; }
    public string? Notes { get; init; }
    public int SortOrder { get; init; }
    public bool IsBuiltIn { get; init; }
    public List<WorkoutTemplateExerciseInput> Exercises { get; init; } = [];
}
public sealed class SaveWorkoutTemplateCommandValidator : AbstractValidator<SaveWorkoutTemplateCommand>
{
    public SaveWorkoutTemplateCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.Exercises).NotEmpty().Must(x => x.Count <= 30);
        RuleForEach(x => x.Exercises).ChildRules(e => { e.RuleFor(x => x.Sets).InclusiveBetween(1, 50); e.RuleFor(x => x.ProgressionType).Must(v => new[] { "None", "IncreaseAllEvenly", "IncreaseLowestSet" }.Contains(v)); });
    }
}
public sealed class SaveWorkoutTemplateCommandHandler : IRequestHandler<SaveWorkoutTemplateCommand, Guid>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public SaveWorkoutTemplateCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task<Guid> Handle(SaveWorkoutTemplateCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        WorkoutTemplate template;
        if (request.Id.HasValue)
        {
            template = await _context.WorkoutTemplates.Include(t => t.Exercises).FirstOrDefaultAsync(t => t.Id == request.Id && (t.UserId == userId || (_currentUser.IsInRole("admin") && t.IsBuiltIn)), ct)
                ?? throw new EntityNotFoundException("Template not found");
            _context.WorkoutTemplateExercises.RemoveRange(template.Exercises);
        }
        else
        {
            template = new WorkoutTemplate { Id = Guid.NewGuid(), UserId = request.IsBuiltIn && _currentUser.IsInRole("admin") ? null : userId, CreatedAt = DateTime.UtcNow };
            _context.WorkoutTemplates.Add(template);
        }
        template.Name = request.Name; template.ProgramName = request.ProgramName; template.SessionOrder = request.SessionOrder;
        template.Notes = request.Notes; template.SortOrder = request.SortOrder; template.IsBuiltIn = request.IsBuiltIn && _currentUser.IsInRole("admin"); template.UpdatedAt = DateTime.UtcNow;
        template.Exercises = request.Exercises.Select(e => Map(template.Id, e)).ToList();
        await _context.SaveChangesAsync(ct);
        return template.Id;
    }
    private static WorkoutTemplateExercise Map(Guid templateId, WorkoutTemplateExerciseInput e) => new()
    {
        Id = Guid.NewGuid(),
        TemplateId = templateId,
        ExerciseId = e.ExerciseId,
        SortOrder = e.SortOrder,
        Sets = e.Sets,
        RepsPerSet = e.RepsPerSet,
        TargetWeightKg = e.TargetWeightKg,
        RestSecondsMin = e.RestSecondsMin,
        RestSecondsMax = e.RestSecondsMax,
        RestSecondsFailure = e.RestSecondsFailure,
        SupersetWithNext = e.SupersetWithNext,
        Notes = e.Notes,
        ProgressionType = e.ProgressionType,
        ProgressionStrategy = e.ProgressionStrategy,
        ProgressionAmountKg = e.ProgressionAmountKg,
        TargetType = e.TargetType,
        TargetSeconds = e.TargetSeconds,
        TargetDistanceMeters = e.TargetDistanceMeters
    };
}

public record DeleteWorkoutTemplateCommand(Guid Id) : IRequest;
public sealed class DeleteWorkoutTemplateCommandHandler : IRequestHandler<DeleteWorkoutTemplateCommand>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public DeleteWorkoutTemplateCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task Handle(DeleteWorkoutTemplateCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var template = await _context.WorkoutTemplates.FirstOrDefaultAsync(t => t.Id == request.Id && (t.UserId == userId || (_currentUser.IsInRole("admin") && t.IsBuiltIn)), ct)
            ?? throw new EntityNotFoundException("Template not found");
        _context.WorkoutTemplates.Remove(template); await _context.SaveChangesAsync(ct);
    }
}

public record DuplicateWorkoutTemplateCommand(Guid Id) : IRequest<Guid>;
public sealed class DuplicateWorkoutTemplateCommandHandler : IRequestHandler<DuplicateWorkoutTemplateCommand, Guid>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public DuplicateWorkoutTemplateCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task<Guid> Handle(DuplicateWorkoutTemplateCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var source = await _context.WorkoutTemplates.AsNoTracking().Include(t => t.Exercises).FirstOrDefaultAsync(t => t.Id == request.Id && (t.IsBuiltIn || t.UserId == userId), ct)
            ?? throw new EntityNotFoundException("Template not found");
        var copy = new WorkoutTemplate { Id = Guid.NewGuid(), UserId = userId, Name = $"{source.Name} Copy", ProgramName = source.ProgramName, SessionOrder = source.SessionOrder, Notes = source.Notes, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        copy.Exercises = source.Exercises.Select(e => new WorkoutTemplateExercise { Id = Guid.NewGuid(), TemplateId = copy.Id, ExerciseId = e.ExerciseId, SortOrder = e.SortOrder, Sets = e.Sets, RepsPerSet = e.RepsPerSet, TargetWeightKg = e.TargetWeightKg, RestSecondsMin = e.RestSecondsMin, RestSecondsMax = e.RestSecondsMax, RestSecondsFailure = e.RestSecondsFailure, SupersetWithNext = e.SupersetWithNext, Notes = e.Notes, ProgressionType = e.ProgressionType, ProgressionStrategy = e.ProgressionStrategy, ProgressionAmountKg = e.ProgressionAmountKg, TargetType = e.TargetType, TargetSeconds = e.TargetSeconds, TargetDistanceMeters = e.TargetDistanceMeters }).ToList();
        _context.WorkoutTemplates.Add(copy); await _context.SaveChangesAsync(ct); return copy.Id;
    }
}
