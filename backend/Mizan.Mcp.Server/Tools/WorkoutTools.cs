using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Mizan.Contracts.Workouts;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class WorkoutTools
{
    private readonly IBackendApiClient _api;
    public WorkoutTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "list_workouts", ReadOnly = true, Idempotent = true)]
    [Description("List workout history with per-set details.")]
    public Task<string> ListWorkouts(int page = 1, int pageSize = 20, CancellationToken ct = default)
        => _api.GetAsync($"/api/Workouts?page={Math.Max(1, page)}&pageSize={Math.Clamp(pageSize, 1, 100)}", ct);

    [McpServerTool(Name = "get_workout", ReadOnly = true, Idempotent = true)]
    [Description("Get one workout by UUID.")]
    public Task<string> GetWorkout(string id, CancellationToken ct = default) => _api.GetAsync($"/api/Workouts/{id}", ct);

    [McpServerTool(Name = "log_workout", Idempotent = false)]
    [Description("Log a workout. exercisesJson must be an array of {exerciseId,notes?,supersetWithNext?,sets:[{reps?,weightKg?,durationSeconds?,distanceMeters?,completedAt?,completed?}]}.")]
    public Task<string> LogWorkout(string name, string workoutDate, string exercisesJson, int? durationMinutes = null,
        decimal? bodyweightKg = null, string? notes = null, string? templateId = null, CancellationToken ct = default)
        => _api.PostAsync("/api/Workouts", new LogWorkoutRequest
        {
            Name = name,
            WorkoutDate = ToolArguments.ParseDate(workoutDate, "workoutDate"),
            DurationMinutes = durationMinutes,
            BodyweightKg = bodyweightKg,
            Notes = notes,
            TemplateId = ToolArguments.ParseOptionalId(templateId, "templateId"),
            Exercises = ParseExercises(exercisesJson),
        }, ct);

    [McpServerTool(Name = "update_workout", Idempotent = true)]
    [Description("Update a workout using its full JSON contract.")]
    public Task<string> UpdateWorkout(string id, string body, CancellationToken ct = default)
        => _api.PutAsync($"/api/Workouts/{id}", JsonSerializer.Deserialize<object>(body)!, ct);

    [McpServerTool(Name = "delete_workout", Destructive = true)]
    [Description("Delete an owned workout.")]
    public Task<string> DeleteWorkout(string id, CancellationToken ct = default) => _api.DeleteAsync($"/api/Workouts/{id}", ct);

    [McpServerTool(Name = "get_workout_stats", ReadOnly = true, Idempotent = true)]
    [Description("Get workout trends, volume, personal-record inputs, and muscle-group coverage.")]
    public Task<string> Stats(string? from = null, string? to = null, CancellationToken ct = default)
        => _api.GetAsync($"/api/Workouts/stats?from={from}&to={to}", ct);

    [McpServerTool(Name = "get_workout_draft", ReadOnly = true, Idempotent = true)]
    public Task<string> Draft(CancellationToken ct = default) => _api.GetAsync("/api/Workouts/draft", ct);

    [McpServerTool(Name = "save_workout_draft", Idempotent = true)]
    public Task<string> SaveDraft(string payloadJson, CancellationToken ct = default) => _api.PutAsync("/api/Workouts/draft", new { payload = payloadJson }, ct);

    [McpServerTool(Name = "delete_workout_draft", Destructive = true)]
    public Task<string> DeleteDraft(CancellationToken ct = default) => _api.DeleteAsync("/api/Workouts/draft", ct);


    /// <summary>
    /// Deserialising into the shared contract rather than a JsonElement is the
    /// point of Mizan.Contracts: a caller that sends the wrong shape is told
    /// so here, instead of the API returning 400 and the tool reporting
    /// success. Case-insensitive because agents write camelCase and
    /// PascalCase interchangeably.
    /// </summary>
    private static List<WorkoutExerciseDto> ParseExercises(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<WorkoutExerciseDto>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid exercisesJson: {ex.Message}");
        }
    }
}
