using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

public record UpdateExerciseCommand(Guid Id, string Name, string Category, string? MuscleGroup, string? Equipment, string? Description, string? VideoUrl, string? ImageUrl) : IRequest;
public sealed class UpdateExerciseCommandValidator : AbstractValidator<UpdateExerciseCommand>
{
    public UpdateExerciseCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Category).Must(value => new[] { "Strength", "Cardio", "Flexibility", "Balance" }.Contains(value, StringComparer.OrdinalIgnoreCase));
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}
public sealed class UpdateExerciseCommandHandler : IRequestHandler<UpdateExerciseCommand>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public UpdateExerciseCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task Handle(UpdateExerciseCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var exercise = await _context.Exercises.FirstOrDefaultAsync(e => e.Id == request.Id && (e.CreatedByUserId == userId || _currentUser.IsInRole("admin")), ct)
            ?? throw new InvalidOperationException("Exercise not found");
        exercise.Name = request.Name;
        exercise.Category = char.ToUpperInvariant(request.Category[0]) + request.Category[1..].ToLowerInvariant();
        exercise.MuscleGroup = request.MuscleGroup; exercise.Equipment = request.Equipment; exercise.Description = request.Description;
        exercise.VideoUrl = request.VideoUrl; exercise.ImageUrl = request.ImageUrl;
        await _context.SaveChangesAsync(ct);
    }
}

public record DeleteExerciseCommand(Guid Id) : IRequest;
public sealed class DeleteExerciseCommandHandler : IRequestHandler<DeleteExerciseCommand>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public DeleteExerciseCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task Handle(DeleteExerciseCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var exercise = await _context.Exercises.FirstOrDefaultAsync(e => e.Id == request.Id && (e.CreatedByUserId == userId || _currentUser.IsInRole("admin")), ct)
            ?? throw new InvalidOperationException("Exercise not found");
        if (await _context.WorkoutExercises.AnyAsync(e => e.ExerciseId == request.Id, ct)) throw new InvalidOperationException("Exercise is used by workouts");
        _context.Exercises.Remove(exercise); await _context.SaveChangesAsync(ct);
    }
}

public record PromoteExerciseCommand(Guid Id) : IRequest;
public sealed class PromoteExerciseCommandHandler : IRequestHandler<PromoteExerciseCommand>
{
    private readonly IMizanDbContext _context; private readonly ICurrentUserService _currentUser;
    public PromoteExerciseCommandHandler(IMizanDbContext context, ICurrentUserService currentUser) { _context = context; _currentUser = currentUser; }
    public async Task Handle(PromoteExerciseCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInRole("admin")) throw new UnauthorizedAccessException();
        var exercise = await _context.Exercises.FirstOrDefaultAsync(e => e.Id == request.Id, ct) ?? throw new InvalidOperationException("Exercise not found");
        exercise.IsCustom = false; exercise.IsApproved = true; exercise.CreatedByUserId = null;
        await _context.SaveChangesAsync(ct);
    }
}
