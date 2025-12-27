using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record RecordGoalProgressCommand : IRequest<RecordGoalProgressResult>
{
    public int ActualCalories { get; init; }
    public decimal ActualProteinGrams { get; init; }
    public decimal ActualCarbsGrams { get; init; }
    public decimal ActualFatGrams { get; init; }
    public decimal? ActualWeight { get; init; }
    public DateOnly? Date { get; init; }
    public string? Notes { get; init; }
}

public record RecordGoalProgressResult(bool Success, string? Message = null, Guid? Id = null);

public class RecordGoalProgressHandler : IRequestHandler<RecordGoalProgressCommand, RecordGoalProgressResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public RecordGoalProgressHandler(IMizanDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<RecordGoalProgressResult> Handle(RecordGoalProgressCommand request, CancellationToken cancellationToken)
    {
        var userIdNullable = _currentUserService.UserId;
        if (!userIdNullable.HasValue || userIdNullable.Value == Guid.Empty)
        {
            return new RecordGoalProgressResult(false, "User not authenticated");
        }

        var userId = userIdNullable.Value;

        // Get user's active goal
        var activeGoal = await _context.UserGoals
            .Where(g => g.UserId == userId && g.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeGoal == null)
        {
            return new RecordGoalProgressResult(false, "No active goal found. Please create a goal first.");
        }

        var date = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // Check if progress already exists for this date
        var existingProgress = await _context.GoalProgress
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Date == date, cancellationToken);

        if (existingProgress != null)
        {
            // Update existing progress
            existingProgress.ActualCalories = request.ActualCalories;
            existingProgress.ActualProteinGrams = request.ActualProteinGrams;
            existingProgress.ActualCarbsGrams = request.ActualCarbsGrams;
            existingProgress.ActualFatGrams = request.ActualFatGrams;
            existingProgress.ActualWeight = request.ActualWeight;
            existingProgress.Notes = request.Notes;

            await _context.SaveChangesAsync(cancellationToken);
            return new RecordGoalProgressResult(true, "Progress updated successfully", existingProgress.Id);
        }

        // Create new progress record
        var progress = new GoalProgress
        {
            Id = Guid.NewGuid(),
            UserGoalId = activeGoal.Id,
            UserId = userId,
            ActualCalories = request.ActualCalories,
            ActualProteinGrams = request.ActualProteinGrams,
            ActualCarbsGrams = request.ActualCarbsGrams,
            ActualFatGrams = request.ActualFatGrams,
            ActualWeight = request.ActualWeight,
            Date = date,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _context.GoalProgress.Add(progress);
        await _context.SaveChangesAsync(cancellationToken);

        return new RecordGoalProgressResult(true, "Progress recorded successfully", progress.Id);
    }
}
