using MediatR;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record LogBodyMeasurementCommand(
    Guid UserId,
    DateTime Date,
    decimal? WeightKg,
    decimal? BodyFatPercentage,
    decimal? MuscleMassKg,
    decimal? WaistCm,
    decimal? HipsCm,
    decimal? ChestCm,
    decimal? ArmsCm,
    decimal? ThighsCm,
    string? Notes
) : IRequest<Guid>;

public class LogBodyMeasurementCommandHandler : IRequestHandler<LogBodyMeasurementCommand, Guid>
{
    private readonly IMizanDbContext _context;

    public LogBodyMeasurementCommandHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(LogBodyMeasurementCommand request, CancellationToken cancellationToken)
    {
        var measurement = new BodyMeasurement
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            MeasurementDate = DateOnly.FromDateTime(request.Date),
            WeightKg = request.WeightKg,
            BodyFatPercentage = request.BodyFatPercentage,
            MuscleMassKg = request.MuscleMassKg,
            WaistCm = request.WaistCm,
            HipsCm = request.HipsCm,
            ChestCm = request.ChestCm,
            ArmsCm = request.ArmsCm,
            ThighsCm = request.ThighsCm,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _context.BodyMeasurements.Add(measurement);
        await _context.SaveChangesAsync(cancellationToken);

        return measurement.Id;
    }
}
