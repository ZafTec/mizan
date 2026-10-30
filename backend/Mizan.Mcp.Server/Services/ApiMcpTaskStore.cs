using System.Text.Json;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;

namespace Mizan.Mcp.Server.Services;

/// <summary>
/// Keeps task state in the API's database, so a task survives a restart of this
/// service and any instance can answer a poll. The caller is the ambient identity
/// (see McpCallIdentity); the API shows a task only to the user who started it.
/// </summary>
public sealed class ApiMcpTaskStore : IMcpTaskStore
{
    private readonly Func<IServiceScopeFactory> _scopes;

    /// <summary>The store is built while the server is configured, before the container exists, so it asks for the scope factory when first used.</summary>
    public ApiMcpTaskStore(Func<IServiceScopeFactory> scopes) => _scopes = scopes;

#pragma warning disable CS0067 // Tasks that ask the user for input are not used here.
    public event Action<InputResponseReceivedEventArgs>? InputResponseReceived;
#pragma warning restore CS0067

    public async Task<McpTaskInfo> CreateTaskAsync(CancellationToken cancellationToken = default) =>
        ToInfo(await SendAsync(api => api.PostAsync("/api/McpTasks", null, cancellationToken)));

    public async Task<McpTaskInfo?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        try { return ToInfo(await SendAsync(api => api.GetAsync($"/api/McpTasks/{Uri.EscapeDataString(taskId)}", cancellationToken))); }
        catch (BackendApiException ex) when (ex.Status == System.Net.HttpStatusCode.NotFound) { return null; }
    }

    public Task SetCompletedAsync(string taskId, JsonElement result, CancellationToken cancellationToken = default) =>
        SendAsync(api => api.PostAsync($"/api/McpTasks/{Uri.EscapeDataString(taskId)}/complete", result, cancellationToken));

    public Task SetFailedAsync(string taskId, JsonElement error, CancellationToken cancellationToken = default) =>
        SendAsync(api => api.PostAsync($"/api/McpTasks/{Uri.EscapeDataString(taskId)}/fail", error, cancellationToken));

    public async Task<bool> SetCancelledAsync(string taskId, CancellationToken cancellationToken = default)
    {
        try { await SendAsync(api => api.PostAsync($"/api/McpTasks/{Uri.EscapeDataString(taskId)}/cancel", null, cancellationToken)); return true; }
        catch (BackendApiException ex) when (ex.Status == System.Net.HttpStatusCode.Conflict) { return false; }
    }

    public Task ResolveInputRequestsAsync(string taskId, IDictionary<string, InputResponse> responses, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SetInputRequestsAsync(string taskId, IDictionary<string, InputRequest> requests, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    private async Task<string> SendAsync(Func<IBackendApiClient, Task<string>> call)
    {
        // The request that started a task may be over by the time its result arrives,
        // so the client comes from a scope of its own.
        using var scope = _scopes().CreateScope();
        return await call(scope.ServiceProvider.GetRequiredService<IBackendApiClient>());
    }

    private static McpTaskInfo ToInfo(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var status = root.GetProperty("status").GetString() switch
        {
            "completed" => McpTaskStatus.Completed,
            "failed" => McpTaskStatus.Failed,
            "cancelled" => McpTaskStatus.Cancelled,
            _ => McpTaskStatus.Working,
        };

        JsonElement? Optional(string name) =>
            root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.Clone() : null;

        return new McpTaskInfo(
            root.GetProperty("id").GetString()!,
            status,
            root.GetProperty("createdAt").GetDateTimeOffset(),
            root.GetProperty("updatedAt").GetDateTimeOffset(),
            TimeSpan.FromSeconds(root.GetProperty("ttlSeconds").GetInt32()),
            root.GetProperty("pollIntervalMs").GetInt64(),
            root.TryGetProperty("statusMessage", out var message) ? message.GetString() : null,
            Optional("result"),
            Optional("error"),
            null);
    }
}
