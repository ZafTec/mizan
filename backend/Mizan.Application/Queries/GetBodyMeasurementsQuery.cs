using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetBodyMeasurementsQuery(Guid UserId) : IRequest<List<BodyMeasurementDto>>;

public record BodyMeasurementDto(
    Guid Id,
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
);

public class GetBodyMeasurementsQueryHandler : IRequestHandler<GetBodyMeasurementsQuery, List<BodyMeasurementDto>>
{
    private readonly IMizanDbContext _context;

    public GetBodyMeasurementsQueryHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<List<BodyMeasurementDto>> Handle(GetBodyMeasurementsQuery request, CancellationToken cancellationToken)
    {
        var measurements = await _context.BodyMeasurements
            .Where(m => m.UserId == request.UserId)
            .OrderByDescending(m => m.MeasurementDate)
            .Select(m => new BodyMeasurementDto(
                m.Id,
                m.MeasurementDate.ToDateTime(TimeOnly.MinValue),
                m.WeightKg,
                m.BodyFatPercentage,
                m.MuscleMassKg,
                m.WaistCm,
                m.HipsCm,
                m.ChestCm,
                m.ArmsCm,
                m.ThighsCm,
                m.Notes
            ))
            .ToListAsync(cancellationToken);

        return measurements;
    }
}
