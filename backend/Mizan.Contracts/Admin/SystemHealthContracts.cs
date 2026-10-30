namespace Mizan.Contracts.Admin;

/// <summary>
/// One line of GET /api/admin/overview/health. Status is ok, degraded, down or
/// notConfigured; notConfigured means there is nothing to measure, and the
/// detail says what is missing rather than showing a number.
/// </summary>
public record SystemHealthCheckDto(string Name, string Status, string Detail, DateTime MeasuredAt);

public record SystemHealthDto(IReadOnlyList<SystemHealthCheckDto> Checks, DateTime MeasuredAt);
