using FluentValidation;
using MediatR;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record CreateExerciseCommand : IRequest<CreateExerciseResult>
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Category { get; init; } = string.Empty;
    public string? MuscleGroup { get; init; }
    public string? Equipment { get; init; }
    public string? VideoUrl { get; init; }
    public string? ImageUrl { get; init; }
}

public record CreateExerciseResult
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool Success { get; init; }
}

public class CreateExerciseCommandValidator : AbstractValidator<CreateExerciseCommand>
{
    public CreateExerciseCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Category).NotEmpty().Must(value => new[] { "Strength", "Cardio", "Flexibility", "Balance" }.Contains(value, StringComparer.OrdinalIgnoreCase));
        RuleFor(x => x.MuscleGroup).MaximumLength(100);
        RuleFor(x => x.Equipment).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.VideoUrl).MaximumLength(500).Must(BeHttpsUrl).When(x => !string.IsNullOrWhiteSpace(x.VideoUrl));
        RuleFor(x => x.ImageUrl).MaximumLength(500).Must(BeHttpsUrl).When(x => !string.IsNullOrWhiteSpace(x.ImageUrl));
    }

    private static bool BeHttpsUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

}

public class CreateExerciseCommandHandler : IRequestHandler<CreateExerciseCommand, CreateExerciseResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateExerciseCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CreateExerciseResult> Handle(CreateExerciseCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Category = char.ToUpperInvariant(request.Category[0]) + request.Category[1..].ToLowerInvariant(),
            MuscleGroup = request.MuscleGroup,
            Equipment = request.Equipment,
            VideoUrl = request.VideoUrl,
            ImageUrl = request.ImageUrl,
            IsCustom = true,
            IsApproved = false,
            CreatedByUserId = _currentUser.UserId.Value,
            CreatedAt = DateTime.UtcNow
        };

        _context.Exercises.Add(exercise);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateExerciseResult
        {
            Id = exercise.Id,
            Name = exercise.Name,
            Success = true
        };
    }
}
