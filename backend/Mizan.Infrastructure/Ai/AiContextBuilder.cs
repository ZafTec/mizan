using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Ai;

namespace Mizan.Infrastructure.Ai;

public class AiContextBuilder : IAiContextBuilder
{
    private const int TrailingDays = 7;
    private const int RecentWorkouts = 5;

    private readonly IMizanDbContext _context;
    private readonly IDataAccessPolicy _policy;

    public AiContextBuilder(IMizanDbContext context, IDataAccessPolicy policy)
    {
        _context = context;
        _policy = policy;
    }

    public async Task<AiContext> BuildAsync(
        Guid principalId,
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        var axes = await _policy.ReadableAxesAsync(
            principalId, subjectId, AccessPurpose.AiContext, cancellationToken);

        var householdId = await ActiveHouseholdAsync(subjectId, cancellationToken);
        if (axes.Count == 0)
        {
            return new AiContext(string.Empty, householdId, Array.Empty<string>());
        }

        var since = DateTime.UtcNow.Date.AddDays(-TrailingDays);
        var summary = new StringBuilder();
        var included = new List<string>();

        if (axes.Contains(DataAxis.Nutrition))
        {
            summary.Append(await NutritionAsync(subjectId, since, cancellationToken));
            included.Add("nutrition");
        }

        if (axes.Contains(DataAxis.Training))
        {
            summary.Append(await TrainingAsync(subjectId, since, cancellationToken));
            included.Add("training");
        }

        if (axes.Contains(DataAxis.Body))
        {
            summary.Append(await BodyAsync(subjectId, cancellationToken));
            included.Add("body");
        }

        return new AiContext(summary.ToString(), householdId, included);
    }

    private async Task<Guid?> ActiveHouseholdAsync(Guid userId, CancellationToken cancellationToken) =>
        await _context.UserHouseholdPreferences.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.ActiveHouseholdId)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<string> NutritionAsync(Guid userId, DateTime since, CancellationToken cancellationToken)
    {
        var days = await _context.FoodDiaryEntries.AsNoTracking()
            .Where(e => e.UserId == userId && e.LoggedAt >= since)
            .GroupBy(e => e.EntryDate)
            .Select(g => new
            {
                Date = g.Key,
                Calories = g.Sum(e => (decimal?)e.Calories) ?? 0m,
                Protein = g.Sum(e => (decimal?)e.ProteinGrams) ?? 0m,
            })
            .OrderByDescending(d => d.Date)
            .ToListAsync(cancellationToken);

        var goal = await _context.UserGoals.AsNoTracking()
            .Where(g => g.UserId == userId && g.IsActive)
            .Select(g => new { g.TargetCalories, g.TargetProteinGrams })
            .FirstOrDefaultAsync(cancellationToken);

        var text = new StringBuilder("## Nutrition (last 7 days)\n");
        if (goal is not null)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"Targets: {goal.TargetCalories} kcal, {goal.TargetProteinGrams} g protein per day.\n");
        }

        if (days.Count == 0)
        {
            text.Append("Nothing logged.\n");
            return text.ToString();
        }

        foreach (var day in days)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"{day.Date:yyyy-MM-dd}: {Math.Round(day.Calories)} kcal, {Math.Round(day.Protein)} g protein\n");
        }

        return text.ToString();
    }

    private async Task<string> TrainingAsync(Guid userId, DateTime since, CancellationToken cancellationToken)
    {
        var workouts = await _context.Workouts.AsNoTracking()
            .Where(w => w.UserId == userId && w.WorkoutDate >= DateOnly.FromDateTime(since))
            .OrderByDescending(w => w.WorkoutDate)
            .Take(RecentWorkouts)
            .Select(w => new { w.WorkoutDate, w.Name, w.DurationMinutes })
            .ToListAsync(cancellationToken);

        var text = new StringBuilder("## Training (last 7 days)\n");
        if (workouts.Count == 0)
        {
            text.Append("Nothing logged.\n");
            return text.ToString();
        }

        foreach (var workout in workouts)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"{workout.WorkoutDate:yyyy-MM-dd}: {workout.Name ?? "Workout"}");
            if (workout.DurationMinutes is { } minutes)
            {
                text.Append(CultureInfo.InvariantCulture, $", {minutes} min");
            }
            text.Append('\n');
        }

        return text.ToString();
    }

    private async Task<string> BodyAsync(Guid userId, CancellationToken cancellationToken)
    {
        var recent = await _context.BodyMeasurements.AsNoTracking()
            .Where(m => m.UserId == userId && m.WeightKg != null)
            .OrderByDescending(m => m.MeasurementDate)
            .Take(2)
            .Select(m => new { m.MeasurementDate, m.WeightKg })
            .ToListAsync(cancellationToken);

        var text = new StringBuilder("## Body\n");
        if (recent.Count == 0)
        {
            text.Append("Nothing logged.\n");
            return text.ToString();
        }

        var latest = recent[0];
        text.Append(CultureInfo.InvariantCulture,
            $"Latest weight: {latest.WeightKg} kg on {latest.MeasurementDate:yyyy-MM-dd}\n");

        if (recent.Count == 2 && recent[1].WeightKg is { } previous && latest.WeightKg is { } current)
        {
            var delta = current - previous;
            var direction = delta >= 0 ? "up" : "down";
            text.Append(CultureInfo.InvariantCulture,
                $"Change since {recent[1].MeasurementDate:yyyy-MM-dd}: {direction} {Math.Abs(delta)} kg\n");
        }

        return text.ToString();
    }
}
