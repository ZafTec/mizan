using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Application.Queries;
using Mizan.Contracts.Mcp;
using Mizan.Domain.Entities;
using Mizan.Domain.Streaks;

namespace Mizan.Application.Commands;

/// <summary>
/// A day's check-in against the active goal (<see cref="Mizan.Contracts.Users.RecordGoalProgressRequest"/>).
/// An actual the caller leaves out comes from that day's diary, and a weight left out from that day's body
/// measurement, so no surface adds anything up and a check-in never records zero for a day that has food.
/// </summary>
public record RecordGoalProgressCommand : IRequest<RecordGoalProgressResult>
{
    public decimal? ActualCalories { get; init; }
    public decimal? ActualProteinGrams { get; init; }
    public decimal? ActualCarbsGrams { get; init; }
    public decimal? ActualFatGrams { get; init; }
    /// <summary>Kilograms.</summary>
    public decimal? ActualWeight { get; init; }
    /// <summary>Defaults to today in the person's time zone.</summary>
    public DateOnly? Date { get; init; }
    public string? Notes { get; init; }
}

public record RecordGoalProgressResult(bool Success, string? Message = null, Guid? Id = null)
{
    /// <summary>What was stored, so the caller can say it back.</summary>
    public GoalProgressEntryDto? Entry { get; init; }
}

public class RecordGoalProgressHandler : IRequestHandler<RecordGoalProgressCommand, RecordGoalProgressResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAchievementEvaluator? _achievements;

    public RecordGoalProgressHandler(IMizanDbContext context, ICurrentUserService currentUserService, IAchievementEvaluator? achievements = null)
    {
        _context = context;
        _currentUserService = currentUserService;
        _achievements = achievements;
    }

    public async Task<RecordGoalProgressResult> Handle(RecordGoalProgressCommand request, CancellationToken cancellationToken)
    {
        var userIdNullable = _currentUserService.UserId;
        if (!userIdNullable.HasValue || userIdNullable.Value == Guid.Empty)
        {
            return new RecordGoalProgressResult(false, "User not authenticated");
        }

        var userId = userIdNullable.Value;

        // A weight is body data. A connected app allowed only nutrition may record the day's food, not a weight.
        if (request.ActualWeight.HasValue && _currentUserService.Grant is { } grant && !grant.Allows(McpScopes.BodyWrite))
        {
            return new RecordGoalProgressResult(false,
                "Recording a weight needs permission to change body measurements. Allow it under Connected apps, or leave the weight out.");
        }

        if (request.ActualWeight is <= 0 or > 700)
            return new RecordGoalProgressResult(false, "Weight must be between 0 and 700 kg.");

        var activeGoal = await _context.UserGoals
            .Where(g => g.UserId == userId && g.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeGoal == null)
        {
            return new RecordGoalProgressResult(false, "No active goal found. Please create a goal first.");
        }

        var zone = await _context.Users.Where(u => u.Id == userId).Select(u => u.TimeZoneId).FirstOrDefaultAsync(cancellationToken);
        var date = request.Date ?? StreakClock.Today(zone, DateTimeOffset.UtcNow);

        var diary = await _context.FoodDiaryEntries
            .Where(e => e.UserId == userId && e.EntryDate == date)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Calories = g.Sum(e => e.Calories ?? 0),
                Protein = g.Sum(e => e.ProteinGrams ?? 0),
                Carbs = g.Sum(e => e.CarbsGrams ?? 0),
                Fat = g.Sum(e => e.FatGrams ?? 0),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var weight = request.ActualWeight ?? await _context.BodyMeasurements
            .Where(m => m.UserId == userId && m.MeasurementDate == date && m.WeightKg != null)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => m.WeightKg)
            .FirstOrDefaultAsync(cancellationToken);

        var progress = await _context.GoalProgress
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Date == date, cancellationToken);
        var updating = progress is not null;

        if (progress is null)
        {
            progress = new GoalProgress
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Date = date,
                CreatedAt = DateTime.UtcNow,
            };
            _context.GoalProgress.Add(progress);
        }

        progress.UserGoalId = activeGoal.Id;
        progress.ActualCalories = request.ActualCalories ?? diary?.Calories ?? 0;
        progress.ActualProteinGrams = request.ActualProteinGrams ?? diary?.Protein ?? 0;
        progress.ActualCarbsGrams = request.ActualCarbsGrams ?? diary?.Carbs ?? 0;
        progress.ActualFatGrams = request.ActualFatGrams ?? diary?.Fat ?? 0;
        // A check-in without a weight keeps the one already recorded for the day.
        progress.ActualWeight = weight ?? progress.ActualWeight;
        progress.Notes = request.Notes ?? progress.Notes;

        await _context.SaveChangesAsync(cancellationToken);
        if (_achievements is not null) await _achievements.EvaluateAsync(cancellationToken, ["goal_progress_logged"]);

        return new RecordGoalProgressResult(true, updating ? "Progress updated successfully" : "Progress recorded successfully", progress.Id)
        {
            Entry = new GoalProgressEntryDto
            {
                Id = progress.Id,
                Date = progress.Date,
                ActualCalories = progress.ActualCalories,
                ActualProteinGrams = progress.ActualProteinGrams,
                ActualCarbsGrams = progress.ActualCarbsGrams,
                ActualFatGrams = progress.ActualFatGrams,
                ActualWeight = progress.ActualWeight,
                Notes = progress.Notes,
            },
        };
    }
}
