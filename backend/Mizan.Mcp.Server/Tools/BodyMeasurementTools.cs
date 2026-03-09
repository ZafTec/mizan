using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class BodyMeasurementTools
{
    private readonly IBackendApiClient _api;

    public BodyMeasurementTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "list_body_measurements", ReadOnly = true, Idempotent = true)]
    [Description("List body measurements (weight, body fat, muscle mass, circumferences) over time.")]
    public async Task<string> ListMeasurements(
        [Description("Page number (default 1)")] int page = 1,
        [Description("Results per page (default 20)")] int pageSize = 20,
        [Description("Sort by field (optional)")] string? sortBy = null,
        [Description("Sort direction: asc or desc")] string? sortOrder = null,
        CancellationToken ct = default)
    {
        var qs = $"/api/BodyMeasurements?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(sortBy)) qs += $"&sortBy={sortBy}";
        if (!string.IsNullOrEmpty(sortOrder)) qs += $"&sortOrder={sortOrder}";
        return await _api.GetAsync(qs, ct);
    }

    [McpServerTool(Name = "log_body_measurement")]
    [Description("Log a body measurement entry. All fields except date are optional - log what you measured.")]
    public async Task<string> LogMeasurement(
        [Description("Measurement date ISO format (defaults to now)")] string? date = null,
        [Description("Weight in kg")] decimal? weightKg = null,
        [Description("Body fat percentage")] decimal? bodyFatPercentage = null,
        [Description("Muscle mass in kg")] decimal? muscleMassKg = null,
        [Description("Waist circumference in cm")] decimal? waistCm = null,
        [Description("Hips circumference in cm")] decimal? hipsCm = null,
        [Description("Chest circumference in cm")] decimal? chestCm = null,
        [Description("Left arm circumference in cm")] decimal? leftArmCm = null,
        [Description("Right arm circumference in cm")] decimal? rightArmCm = null,
        [Description("Left thigh circumference in cm")] decimal? leftThighCm = null,
        [Description("Right thigh circumference in cm")] decimal? rightThighCm = null,
        [Description("Notes (optional)")] string? notes = null,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/BodyMeasurements", new
        {
            date, weightKg, bodyFatPercentage, muscleMassKg,
            waistCm, hipsCm, chestCm, leftArmCm, rightArmCm,
            leftThighCm, rightThighCm, notes
        }, ct);
    }

    [McpServerTool(Name = "delete_body_measurement", Destructive = true)]
    [Description("Delete a body measurement entry. This is permanent.")]
    public async Task<string> DeleteMeasurement(
        [Description("Measurement UUID")] string id,
        CancellationToken ct = default)
    {
        return await _api.DeleteAsync($"/api/BodyMeasurements/{id}", ct);
    }
}
