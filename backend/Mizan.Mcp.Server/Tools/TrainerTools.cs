using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class TrainerTools
{
    private readonly IBackendApiClient _api;

    public TrainerTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "list_available_trainers", ReadOnly = true, Idempotent = true)]
    [Description("Browse available trainers that accept new clients.")]
    public async Task<string> ListAvailableTrainers(
        [Description("Page number (default 1)")] int page = 1,
        [Description("Results per page (default 20)")] int pageSize = 20,
        CancellationToken ct = default)
    {
        return await _api.GetAsync($"/api/Trainers/available?page={page}&pageSize={pageSize}", ct);
    }

    [McpServerTool(Name = "send_trainer_request")]
    [Description("Send a request to connect with a trainer.")]
    public async Task<string> SendRequest(
        [Description("Trainer's user UUID")] string trainerId,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/Trainers/request", new { trainerId }, ct);
    }

    [McpServerTool(Name = "get_my_trainer", ReadOnly = true, Idempotent = true)]
    [Description("Get the current user's active trainer relationship.")]
    public async Task<string> GetMyTrainer(CancellationToken ct = default)
    {
        return await _api.GetAsync("/api/Trainers/my-trainer", ct);
    }

    [McpServerTool(Name = "get_my_trainer_requests", ReadOnly = true, Idempotent = true)]
    [Description("Get the current user's pending trainer requests.")]
    public async Task<string> GetMyRequests(
        [Description("Page number (default 1)")] int page = 1,
        [Description("Results per page (default 20)")] int pageSize = 20,
        CancellationToken ct = default)
    {
        return await _api.GetAsync($"/api/Trainers/my-requests?page={page}&pageSize={pageSize}", ct);
    }

    // -- Trainer-only tools (backend enforces RequireTrainer policy) --

    [McpServerTool(Name = "respond_to_trainer_request")]
    [Description("Respond to a client's trainer request (accept or reject). Requires trainer role.")]
    public async Task<string> RespondToRequest(
        [Description("Relationship UUID")] string relationshipId,
        [Description("Accept or reject the request")] bool accept,
        [Description("Allow client to view nutrition data")] bool? canViewNutrition = null,
        [Description("Allow client to view workout data")] bool? canViewWorkouts = null,
        [Description("Allow client to view body measurements")] bool? canViewMeasurements = null,
        [Description("Allow messaging with client")] bool? canMessage = null,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/Trainers/respond", new
        {
            relationshipId, accept, canViewNutrition, canViewWorkouts, canViewMeasurements, canMessage
        }, ct);
    }

    [McpServerTool(Name = "list_trainer_clients", ReadOnly = true, Idempotent = true)]
    [Description("List the trainer's clients. Requires trainer role.")]
    public async Task<string> ListClients(
        [Description("Page number (default 1)")] int page = 1,
        [Description("Results per page (default 20)")] int pageSize = 20,
        CancellationToken ct = default)
    {
        return await _api.GetAsync($"/api/Trainers/clients?page={page}&pageSize={pageSize}", ct);
    }

    [McpServerTool(Name = "list_trainer_pending_requests", ReadOnly = true, Idempotent = true)]
    [Description("List pending client requests for the trainer. Requires trainer role.")]
    public async Task<string> ListPendingRequests(
        [Description("Page number (default 1)")] int page = 1,
        [Description("Results per page (default 20)")] int pageSize = 20,
        CancellationToken ct = default)
    {
        return await _api.GetAsync($"/api/Trainers/requests?page={page}&pageSize={pageSize}", ct);
    }

    [McpServerTool(Name = "get_client_nutrition", ReadOnly = true, Idempotent = true)]
    [Description("View a client's nutrition data for a specific date. Requires trainer role and permission.")]
    public async Task<string> GetClientNutrition(
        [Description("Client's user UUID")] string clientId,
        [Description("Date in ISO format (optional, defaults to today)")] string? date = null,
        CancellationToken ct = default)
    {
        var qs = $"/api/Trainers/clients/{clientId}/nutrition";
        if (!string.IsNullOrEmpty(date)) qs += $"?date={date}";
        return await _api.GetAsync(qs, ct);
    }
}
