namespace Mizan.Contracts.Users;

/// <summary>
/// The body of <c>POST /api/Goals</c>. Weight is in <see cref="WeightUnit"/> ("kg" or "lb"); every target is optional.
/// </summary>
public sealed record CreateGoalRequest(
    string? GoalType,
    int? TargetCalories,
    decimal? TargetProteinGrams,
    decimal? TargetCarbsGrams,
    decimal? TargetFatGrams,
    decimal? TargetFiberGrams,
    decimal? TargetWeight,
    string? WeightUnit,
    DateOnly? TargetDate);

/// <summary>
/// The body of <c>POST /api/Goals/progress</c>. A day's check-in against the active goal. An actual left out is
/// taken from that day's diary, and a weight left out from that day's body measurement, so a caller sends only
/// what the person said. <see cref="ActualWeight"/> is kilograms and needs body write access.
/// </summary>
public sealed record RecordGoalProgressRequest(
    DateOnly? Date = null,
    decimal? ActualWeight = null,
    string? Notes = null,
    decimal? ActualCalories = null,
    decimal? ActualProteinGrams = null,
    decimal? ActualCarbsGrams = null,
    decimal? ActualFatGrams = null);
