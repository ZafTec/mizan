using FluentValidation;
using MediatR;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record CreateBodyMeasurementCommand : IRequest<CreateBodyMeasurementResult>
{
    public DateOnly MeasurementDate { get; init; }
    public decimal? WeightKg { get; init; }
    public decimal? BodyFatPercentage { get; init; }
    public decimal? MuscleMassKg { get; init; }
    public decimal? WaistCm { get; init; }
    public decimal? HipsCm { get; init; }
    public decimal? ChestCm { get; init; }
    public decimal? ArmsCm { get; init; }
    public decimal? ThighsCm { get; init; }
    public string? Notes { get; init; }
}

public record CreateBodyMeasurementResult
{
    public Guid Id { get; init; }
    public DateOnly MeasurementDate { get; init; }
    public bool Success { get; init; }
}

public class CreateBodyMeasurementCommandValidator : AbstractValidator<CreateBodyMeasurementCommand>
{
    public CreateBodyMeasurementCommandValidator()
    {
        RuleFor(x => x.MeasurementDate).NotEmpty();
        RuleFor(x => x.WeightKg).GreaterThan(0).When(x => x.WeightKg.HasValue);
        RuleFor(x => x.BodyFatPercentage).InclusiveBetween(0, 100).When(x => x.BodyFatPercentage.HasValue);
        RuleFor(x => x.MuscleMassKg).GreaterThan(0).When(x => x.MuscleMassKg.HasValue);
        RuleFor(x => x.WaistCm).GreaterThan(0).When(x => x.WaistCm.HasValue);
        RuleFor(x => x.HipsCm).GreaterThan(0).When(x => x.HipsCm.HasValue);
        RuleFor(x => x.ChestCm).GreaterThan(0).When(x => x.ChestCm.HasValue);
        RuleFor(x => x.ArmsCm).GreaterThan(0).When(x => x.ArmsCm.HasValue);
        RuleFor(x => x.ThighsCm).GreaterThan(0).When(x => x.ThighsCm.HasValue);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public class CreateBodyMeasurementCommandHandler : IRequestHandler<CreateBodyMeasurementCommand, CreateBodyMeasurementResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateBodyMeasurementCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CreateBodyMeasurementResult> Handle(CreateBodyMeasurementCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var measurement = new BodyMeasurement
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId.Value,
            MeasurementDate = request.MeasurementDate,
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

        return new CreateBodyMeasurementResult
        {
            Id = measurement.Id,
            MeasurementDate = measurement.MeasurementDate,
            Success = true
        };
    }
}
