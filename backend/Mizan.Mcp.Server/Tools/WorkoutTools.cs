using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class WorkoutTools
{
    private readonly IBackendApiClient _api;

    public WorkoutTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "log_workout")]
    [Description("Log a workout session with exercises, sets, reps, and weight.")]
    public async Task<string> LogWorkout(
        [Description("JSON body matching the LogWorkoutCommand schema. Include exerciseId, sets, reps, weight, duration, notes.")] string body,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/Workouts", System.Text.Json.JsonSerializer.Deserialize<object>(body), ct);
    }
}
