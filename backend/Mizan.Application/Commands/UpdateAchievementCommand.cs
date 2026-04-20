using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

public record UpdateAchievementCommand : IRequest<Unit>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? IconUrl { get; init; }
    public int Points { get; init; }
    public string? Category { get; init; }
    public string? CriteriaType { get; init; }
    public int Threshold { get; init; }
}

public class UpdateAchievementCommandValidator : AbstractValidator<UpdateAchievementCommand>
{
    private static readonly string[] KnownCriteria =
    {
        "meals_logged", "recipes_created", "workouts_logged",
        "body_measurements_logged", "goal_progress_logged",
        "streak_nutrition", "streak_workout", "points_total"
    };

    private static readonly string[] KnownCategories =
    {
        "nutrition", "consistency", "workout", "milestone"
    };

    public UpdateAchievementCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.IconUrl).MaximumLength(500);
        RuleFor(x => x.Points).InclusiveBetween(0, 10_000);
        RuleFor(x => x.Threshold).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Category)
            .Must(c => c == null || KnownCategories.Contains(c))
            .WithMessage($"Category must be one of: {string.Join(", ", KnownCategories)}");
        RuleFor(x => x.CriteriaType)
            .Must(c => c == null || KnownCriteria.Contains(c))
            .WithMessage($"CriteriaType must be one of: {string.Join(", ", KnownCriteria)}");
    }
}

public class UpdateAchievementCommandHandler : IRequestHandler<UpdateAchievementCommand, Unit>
{
    private readonly IMizanDbContext _context;

    public UpdateAchievementCommandHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(UpdateAchievementCommand request, CancellationToken cancellationToken)
    {
        var achievement = await _context.Achievements
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("Achievement", request.Id);

        achievement.Name = request.Name.Trim();
        achievement.Description = request.Description?.Trim();
        achievement.IconUrl = request.IconUrl?.Trim();
        achievement.Points = request.Points;
        achievement.Category = request.Category;
        achievement.CriteriaType = request.CriteriaType;
        achievement.Threshold = request.Threshold;

        await _context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
