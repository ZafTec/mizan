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

        var unlocked = await _achievements.EvaluateAsync(cancellationToken);

        return new LogBodyMeasurementResult
        {
            Id = measurement.Id,
            UnlockedAchievements = unlocked
        };
    }
}
