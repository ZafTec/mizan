using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class ExerciseTools
{
    private readonly IBackendApiClient _api; public ExerciseTools(IBackendApiClient api) => _api = api;
    [McpServerTool(Name = "list_exercises", ReadOnly = true, Idempotent = true)]
    public Task<string> ListExercises(string? search = null, string? muscleGroup = null, string? category = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var query = $"/api/Exercises?page={Math.Max(1, page)}&pageSize={Math.Clamp(pageSize, 1, 100)}";
        if (!string.IsNullOrWhiteSpace(search)) query += $"&searchTerm={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrWhiteSpace(muscleGroup)) query += $"&muscleGroup={Uri.EscapeDataString(muscleGroup)}";
        if (!string.IsNullOrWhiteSpace(category)) query += $"&category={Uri.EscapeDataString(category)}";
        return _api.GetAsync(query, ct);
    }
    [McpServerTool(Name = "create_exercise")]
    public Task<string> CreateExercise(string name, string category, string? muscleGroup = null, string? equipment = null, string? description = null, CancellationToken ct = default)
        => _api.PostAsync("/api/Exercises", new { name, category, muscleGroup, equipment, description }, ct);
    [McpServerTool(Name = "update_exercise", Idempotent = true)]
    public Task<string> UpdateExercise(string id, string name, string category, string? muscleGroup = null, string? equipment = null, string? description = null, CancellationToken ct = default)
        => _api.PutAsync($"/api/Exercises/{id}", new { id, name, category, muscleGroup, equipment, description }, ct);
    [McpServerTool(Name = "delete_exercise", Destructive = true)] public Task<string> DeleteExercise(string id, CancellationToken ct = default) => _api.DeleteAsync($"/api/Exercises/{id}", ct);
}
