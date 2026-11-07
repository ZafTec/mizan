using System.ComponentModel;
using Mizan.Contracts.Coaching;
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
    [Description("Respond to a client's trainer request (accept or reject). For coaches (the trainer role).")]
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
            relationshipId,
            accept,
            canViewNutrition,
            canViewWorkouts,
            canViewMeasurements,
            canMessage
        }, ct);
    }

    [McpServerTool(Name = "list_trainer_clients", ReadOnly = true, Idempotent = true)]
    [Description("List the trainer's clients. For coaches (the trainer role).")]
    public async Task<string> ListClients(
        [Description("Page number (default 1)")] int page = 1,
        [Description("Results per page (default 20)")] int pageSize = 20,
        CancellationToken ct = default)
    {
        return await _api.GetAsync($"/api/Trainers/clients?page={page}&pageSize={pageSize}", ct);
    }

    [McpServerTool(Name = "list_trainer_pending_requests", ReadOnly = true, Idempotent = true)]
    [Description("List pending client requests for the trainer. For coaches (the trainer role).")]
    public async Task<string> ListPendingRequests(
        [Description("Page number (default 1)")] int page = 1,
        [Description("Results per page (default 20)")] int pageSize = 20,
        CancellationToken ct = default)
    {
        return await _api.GetAsync($"/api/Trainers/requests?page={page}&pageSize={pageSize}", ct);
    }

    [McpServerTool(Name = "get_client_nutrition", ReadOnly = true, Idempotent = true)]
    [Description("View a client's nutrition data for a specific date. For coaches (the trainer role); the client must have shared their nutrition.")]
    public async Task<string> GetClientNutrition(
        [Description("Client's user UUID")] string clientId,
        [Description("Date in ISO format (optional, defaults to today)")] string? date = null,
        CancellationToken ct = default)
    {
        var qs = $"/api/Trainers/clients/{clientId}/nutrition";
        if (!string.IsNullOrEmpty(date)) qs += $"?date={date}";
        return await _api.GetAsync(qs, ct);
    }

    [McpServerTool(Name = "get_client_workouts", ReadOnly = true, Idempotent = true)]
    [Description("A client's logged workouts, newest first. For coaches (the trainer role); the client must have shared their workouts.")]
    public Task<string> GetClientWorkouts(
        [Description("Client's user UUID")] string clientId,
        [Description("Page, from 1")] int page = 1,
        [Description("Workouts per page, up to 100")] int pageSize = 20,
        CancellationToken ct = default) =>
        _api.GetAsync($"/api/Trainers/clients/{ToolArguments.ParseId(clientId, nameof(clientId))}/workouts?page={page}&pageSize={pageSize}", ct);

    [McpServerTool(Name = "set_trainer_permissions", Idempotent = true)]
    [Description("For a client: choose what their coach may see and do, or end the coaching. A value left out keeps its setting. "
        + "The relationship id is in get_my_trainer.")]
    public Task<string> SetTrainerPermissions(
        [Description("The coaching relationship UUID")] string relationshipId,
        [Description("The coach sees the food diary and nutrition")] bool? canViewNutrition = null,
        [Description("The coach sees workouts")] bool? canViewWorkouts = null,
        [Description("The coach sees body measurements")] bool? canViewMeasurements = null,
        [Description("The coach may message")] bool? canMessage = null,
        [Description("The coach may draft meal plans")] bool? canPlanMeals = null,
        [Description("True ends the coaching. Confirm with the person first.")] bool end = false,
        CancellationToken ct = default) =>
        _api.PatchAsync($"/api/Trainers/relationships/{ToolArguments.ParseId(relationshipId, nameof(relationshipId))}/grants",
            new UpdateGrantsRequest(canViewNutrition, canViewWorkouts, canViewMeasurements, canMessage, end, canPlanMeals), ct);

    [McpServerTool(Name = "get_coach_chat", ReadOnly = true, Idempotent = true)]
    [Description("The messages between a coach and a client, for either of them. The relationship id is in get_my_trainer or list_trainer_clients.")]
    public Task<string> GetChat([Description("The coaching relationship UUID")] string relationshipId, CancellationToken ct = default) =>
        _api.GetAsync($"/api/Chat/{ToolArguments.ParseId(relationshipId, nameof(relationshipId))}", ct);

    [McpServerTool(Name = "send_coach_message")]
    [Description("Send a message in a coach and client conversation. The conversation id is in get_coach_chat. The coach must be allowed to message.")]
    public Task<string> SendChatMessage(
        [Description("The conversation UUID from get_coach_chat")] string conversationId,
        [Description("The message")] string content,
        CancellationToken ct = default) =>
        _api.PostAsync("/api/Chat/send", new SendMessageRequest(ToolArguments.ParseId(conversationId, nameof(conversationId)), content), ct);
}
