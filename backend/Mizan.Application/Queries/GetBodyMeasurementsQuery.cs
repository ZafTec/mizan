using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetBodyMeasurementsQuery : IRequest<PagedResult<BodyMeasurementDto>>, IPagedQuery, ISortableQuery
{
    public Guid UserId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}

public record BodyMeasurementDto(
    Guid Id,
    DateTime Date,
    decimal? WeightKg,
    decimal? BodyFatPercentage,
    decimal? MuscleMassKg,
    decimal? WaistCm,
    decimal? HipsCm,
    decimal? ChestCm,
    decimal? LeftArmCm,
    decimal? RightArmCm,
    decimal? LeftThighCm,
    decimal? RightThighCm,
    string? Notes
);

public class GetBodyMeasurementsQueryHandler : IRequestHandler<GetBodyMeasurementsQuery, PagedResult<BodyMeasurementDto>>
{
    private static readonly Dictionary<string, Expression<Func<Domain.Entities.BodyMeasurement, object>>> SortMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["date"] = m => m.MeasurementDate,
        ["weight"] = m => m.WeightKg!
    };

    private readonly IMizanDbContext _context;

    public GetBodyMeasurementsQueryHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<BodyMeasurementDto>> Handle(GetBodyMeasurementsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.BodyMeasurements
            .Where(m => m.UserId == request.UserId);

        var totalCount = await query.CountAsync(cancellationToken);

        var sortedQuery = query.ApplySorting(
            request,
            SortMappings,
            defaultSort: m => m.MeasurementDate,
            defaultDescending: true);

        var measurements = await sortedQuery
            .ApplyPaging(request)
            .Select(m => new BodyMeasurementDto(
                m.Id,
                m.MeasurementDate.ToDateTime(TimeOnly.MinValue),
                m.WeightKg,
                m.BodyFatPercentage,
                m.MuscleMassKg,
                m.WaistCm,
                m.HipsCm,
                m.ChestCm,
                m.LeftArmCm,
                m.RightArmCm,
                m.LeftThighCm,
                m.RightThighCm,
                m.Notes
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<BodyMeasurementDto>
        {
            Items = measurements,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
