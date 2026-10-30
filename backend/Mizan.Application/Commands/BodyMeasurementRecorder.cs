using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Body;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

/// <summary>
/// Every write of a body measurement goes through here, so the day's row is found and updated
/// (<see cref="BodyMeasurementDay"/>) whichever surface sent it: the measurement form, log_day, or a
/// workout's bodyweight. The caller saves.
/// </summary>
public static class BodyMeasurementRecorder
{
    public static async Task<BodyMeasurement> RecordAsync(
        IMizanDbContext context,
        Guid userId,
        DateOnly date,
        BodyMeasurementValues values,
        string? notes,
        CancellationToken cancellationToken,
        string? noteForNewDay = null)
    {
        var day = context.BodyMeasurements.Local
                .Where(m => m.UserId == userId && m.MeasurementDate == date)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefault()
            ?? await context.BodyMeasurements
                .Where(m => m.UserId == userId && m.MeasurementDate == date)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

        if (day is null)
        {
            day = new BodyMeasurement
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MeasurementDate = date,
                Notes = noteForNewDay,
                CreatedAt = DateTime.UtcNow,
            };
            context.BodyMeasurements.Add(day);
        }

        BodyMeasurementDay.Apply(day, values, notes);
        return day;
    }
}
