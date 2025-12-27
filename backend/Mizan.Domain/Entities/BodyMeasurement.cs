namespace Mizan.Domain.Entities;

public class BodyMeasurement
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly MeasurementDate { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? BodyFatPercentage { get; set; }
    public decimal? MuscleMassKg { get; set; }
    public decimal? WaistCm { get; set; }
    public decimal? HipsCm { get; set; }
    public decimal? ChestCm { get; set; }
    public decimal? ArmsCm { get; set; }
    public decimal? ThighsCm { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation property
    public virtual User User { get; set; } = null!;
}
