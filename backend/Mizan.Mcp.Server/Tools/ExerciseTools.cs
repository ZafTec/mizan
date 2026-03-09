using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class ExerciseTools
{
    private readonly IBackendApiClient _api;

    public ExerciseTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "list_exercises", ReadOnly = true, Idempotent = true)]
    [Description("List available exercises. Can search by name, filter by muscle group or category.")]
    public async Task<string> ListExercises(
        [Description("Search term")] string? search = null,
        [Description("Muscle group filter")] string? muscleGroup = null,
        [Description("Category filter")] string? category = null,
        [Description("Page number (default 1)")] int page = 1,
        [Description("Results per page (default 20)")] int pageSize = 20,
        CancellationToken ct = default)
    {
        var qs = $"/api/Exercises?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search)) qs += $"&search={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrEmpty(muscleGroup)) qs += $"&muscleGroup={Uri.EscapeDataString(muscleGroup)}";
        if (!string.IsNullOrEmpty(category)) qs += $"&category={Uri.EscapeDataString(category)}";
        return await _api.GetAsync(qs, ct);
    }

    [McpServerTool(Name = "create_exercise")]
    [Description("Create a new exercise definition.")]
    public async Task<string> CreateExercise(
        [Description("JSON body matching the CreateExerciseCommand schema. Include name, muscleGroup, category, description.")] string body,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/Exercises", System.Text.Json.JsonSerializer.Deserialize<object>(body), ct);
    }
}
