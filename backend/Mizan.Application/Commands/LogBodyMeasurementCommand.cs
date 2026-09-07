using MediatR;
using FluentValidation;
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
    decimal? LeftArmCm,
    decimal? RightArmCm,
    decimal? LeftThighCm,
    decimal? RightThighCm,
    string? Notes
) : IRequest<LogBodyMeasurementResult>;

public record LogBodyMeasurementResult
{
    public Guid Id { get; init; }
    public IReadOnlyList<UnlockedAchievement> UnlockedAchievements { get; init; } = [];
}

public sealed class LogBodyMeasurementCommandValidator : AbstractValidator<LogBodyMeasurementCommand>
{
    public LogBodyMeasurementCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x).Must(x => new[] { x.WeightKg, x.BodyFatPercentage, x.MuscleMassKg,
                x.WaistCm, x.HipsCm, x.ChestCm, x.LeftArmCm, x.RightArmCm, x.LeftThighCm, x.RightThighCm }.Any(v => v.HasValue))
            .WithMessage("Enter at least one body measurement");
        RuleFor(x => x.WeightKg).GreaterThan(0).When(x => x.WeightKg.HasValue);
        RuleFor(x => x.BodyFatPercentage).InclusiveBetween(0, 100).When(x => x.BodyFatPercentage.HasValue);
        RuleFor(x => x.MuscleMassKg).GreaterThan(0).When(x => x.MuscleMassKg.HasValue);
        RuleFor(x => x.WaistCm).GreaterThan(0).When(x => x.WaistCm.HasValue);
        RuleFor(x => x.HipsCm).GreaterThan(0).When(x => x.HipsCm.HasValue);
        RuleFor(x => x.ChestCm).GreaterThan(0).When(x => x.ChestCm.HasValue);
        RuleFor(x => x.LeftArmCm).GreaterThan(0).When(x => x.LeftArmCm.HasValue);
        RuleFor(x => x.RightArmCm).GreaterThan(0).When(x => x.RightArmCm.HasValue);
        RuleFor(x => x.LeftThighCm).GreaterThan(0).When(x => x.LeftThighCm.HasValue);
        RuleFor(x => x.RightThighCm).GreaterThan(0).When(x => x.RightThighCm.HasValue);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public class LogBodyMeasurementCommandHandler : IRequestHandler<LogBodyMeasurementCommand, LogBodyMeasurementResult>
{
    private readonly IMizanDbContext _context;
    private readonly IAchievementEvaluator _achievements;

    public LogBodyMeasurementCommandHandler(IMizanDbContext context, IAchievementEvaluator achievements)
    {
        _context = context;
        _achievements = achievements;
    }

    public async Task<LogBodyMeasurementResult> Handle(LogBodyMeasurementCommand request, CancellationToken cancellationToken)
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
            LeftArmCm = request.LeftArmCm,
            RightArmCm = request.RightArmCm,
            LeftThighCm = request.LeftThighCm,
            RightThighCm = request.RightThighCm,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _context.BodyMeasurements.Add(measurement);
        await _context.SaveChangesAsync(cancellationToken);

        var unlocked = await _achievements.EvaluateAsync(cancellationToken, ["body_measurements_logged"]);

        return new LogBodyMeasurementResult
        {
            Id = measurement.Id,
            UnlockedAchievements = unlocked
        };
    }
}
