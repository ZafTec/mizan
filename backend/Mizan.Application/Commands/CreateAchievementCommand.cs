using FluentValidation;
using MediatR;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record CreateAchievementCommand : IRequest<CreateAchievementResult>
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? IconUrl { get; init; }
    public int Points { get; init; }
    public string? Category { get; init; }
    public string? CriteriaType { get; init; }
    public int Threshold { get; init; }
}

public record CreateAchievementResult(Guid Id);

public class CreateAchievementCommandValidator : AbstractValidator<CreateAchievementCommand>
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

    public CreateAchievementCommandValidator()
    {
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

public class CreateAchievementCommandHandler : IRequestHandler<CreateAchievementCommand, CreateAchievementResult>
{
    private readonly IMizanDbContext _context;

    public CreateAchievementCommandHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<CreateAchievementResult> Handle(CreateAchievementCommand request, CancellationToken cancellationToken)
    {
        var achievement = new Achievement
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IconUrl = request.IconUrl?.Trim(),
            Points = request.Points,
            Category = request.Category,
            CriteriaType = request.CriteriaType,
            Threshold = request.Threshold
        };

        _context.Achievements.Add(achievement);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateAchievementResult(achievement.Id);
    }
}
