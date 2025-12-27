using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record CreateUserGoalCommand : IRequest<CreateUserGoalResult>
{
    public string? GoalType { get; init; }
    public int? TargetCalories { get; init; }
    public decimal? TargetProteinGrams { get; init; }
    public decimal? TargetCarbsGrams { get; init; }
    public decimal? TargetFatGrams { get; init; }
    public decimal? TargetWeight { get; init; }
    public string? WeightUnit { get; init; }
    public DateOnly? TargetDate { get; init; }
}

public record CreateUserGoalResult
{
    public Guid Id { get; init; }
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public class CreateUserGoalCommandValidator : AbstractValidator<CreateUserGoalCommand>
{
    public CreateUserGoalCommandValidator()
    {
        RuleFor(x => x.GoalType).MaximumLength(100)
            .Must(g => g == null || new[] { "weight_loss", "muscle_gain", "maintenance", "general" }.Contains(g.ToLower()))
            .When(x => x.GoalType != null)
            .WithMessage("Goal type must be weight_loss, muscle_gain, maintenance, or general");
        RuleFor(x => x.TargetCalories).GreaterThan(0).When(x => x.TargetCalories.HasValue);
        RuleFor(x => x.TargetProteinGrams).GreaterThan(0).When(x => x.TargetProteinGrams.HasValue);
        RuleFor(x => x.TargetCarbsGrams).GreaterThan(0).When(x => x.TargetCarbsGrams.HasValue);
        RuleFor(x => x.TargetFatGrams).GreaterThan(0).When(x => x.TargetFatGrams.HasValue);
        RuleFor(x => x.TargetWeight).GreaterThan(0).When(x => x.TargetWeight.HasValue);
        RuleFor(x => x.WeightUnit).MaximumLength(10)
            .Must(u => u == null || new[] { "kg", "lb" }.Contains(u.ToLower()))
            .When(x => x.WeightUnit != null)
            .WithMessage("Weight unit must be kg or lb");
    }
}

public class CreateUserGoalCommandHandler : IRequestHandler<CreateUserGoalCommand, CreateUserGoalResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateUserGoalCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CreateUserGoalResult> Handle(CreateUserGoalCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return new CreateUserGoalResult
            {
                Success = false,
                Message = "User not authenticated"
            };
        }

        // Deactivate existing goals
        var existingGoals = await _context.UserGoals
            .Where(g => g.UserId == _currentUser.UserId && g.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var existing in existingGoals)
        {
            existing.IsActive = false;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        // Create new goal
        var goal = new UserGoal
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId.Value,
            GoalType = request.GoalType,
            TargetCalories = request.TargetCalories,
            TargetProteinGrams = request.TargetProteinGrams,
            TargetCarbsGrams = request.TargetCarbsGrams,
            TargetFatGrams = request.TargetFatGrams,
            TargetWeight = request.TargetWeight,
            WeightUnit = request.WeightUnit ?? "kg",
            TargetDate = request.TargetDate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.UserGoals.Add(goal);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateUserGoalResult
        {
            Id = goal.Id,
            Success = true,
            Message = "Goal created successfully"
        };
    }
}
