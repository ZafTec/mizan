using System.ComponentModel;
using System.Text.Json;
using Mizan.Contracts.Ai;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

/// <summary>
/// The assistant, as tools.
///
/// Every one of these goes through the same API the website does, so the same
/// two rules apply without being restated here: no provider call outside
/// <c>IAiQuotaService</c>, and no personal data without
/// <c>IDataAccessPolicy</c> (docs/AI.md). Consent is default-off
/// and the user owns it - <c>set_ai_consent</c> is the user acting on their own
/// account, which is the only principal an MCP token has.
/// </summary>
[McpServerToolType]
public sealed class AiTools
{
    private readonly IBackendApiClient _api;
    private readonly McpLinks _links;

    public AiTools(IBackendApiClient api, McpLinks links)
    {
        _api = api;
        _links = links;
    }

    // ---- Consent ----------------------------------------------------------

    [McpServerTool(Name = "get_ai_consent", ReadOnly = true, Idempotent = true)]
    [Description(
        "What the assistant is currently allowed to read about you, per axis: "
        + "nutrition, training, body. Default is off for all three.")]
    public Task<string> GetConsent(CancellationToken ct = default) =>
        _api.GetAsync("/api/Ai/consent", ct);

    [McpServerTool(Name = "set_ai_consent")]
    [Description(
        "Sets the assistant's read access. enabled false turns it off entirely "
        + "regardless of the axes. Narrowing an axis takes effect on the next "
        + "question - nothing is retained from earlier ones.")]
    public Task<string> SetConsent(
        bool enabled,
        bool shareNutrition = false,
        bool shareTraining = false,
        bool shareBody = false,
        CancellationToken ct = default) =>
        _api.PutAsync("/api/Ai/consent", new { enabled, shareNutrition, shareTraining, shareBody }, ct);

    // ---- Usage ------------------------------------------------------------

    [McpServerTool(Name = "get_ai_usage", ReadOnly = true, Idempotent = true)]
    [Description("Your own assistant usage and what is left of today's allowance.")]
    public Task<string> GetUsage(
        [Description("How many days of history, 1-90")] int days = 14,
        CancellationToken ct = default) =>
        _api.GetAsync($"/api/Ai/usage?days={Math.Clamp(days, 1, 90)}", ct);

    // ---- Chat -------------------------------------------------------------

    [McpServerTool(Name = "ask_ai")]
    [Description(
        "One turn with the assistant, on the same threads the website uses. "
        + "Omit threadId to start a new one. Costs against your daily allowance "
        + "and answers only from the axes you have consented to share. "
        + "consentRequired lists any permission a tool call was refused for; tell the person where to allow it.")]
    public async Task<CallToolResult> Ask(string message, string? threadId = null, CancellationToken ct = default)
    {
        var json = await _api.PostAsync(
            "/api/Ai/chat",
            new { threadId = ToolArguments.ParseOptionalId(threadId, "threadId"), message },
            ct);
        return AskResult(json, _links);
    }

    /// <summary>
    /// The turn as JSON text and as <c>structuredContent</c>, and, when a tool call was refused for
    /// consent, one line naming the switch and where to turn it on. The API decides the refusal;
    /// this only words it.
    /// </summary>
    internal static CallToolResult AskResult(string json, McpLinks links)
    {
        var result = new CallToolResult { Content = [new TextContentBlock { Text = json }] };

        JsonElement turn;
        try { turn = JsonSerializer.Deserialize<JsonElement>(json); }
        catch (JsonException) { return result; }
        if (turn.ValueKind != JsonValueKind.Object) return result;
        result.StructuredContent = turn;

        if (!turn.TryGetProperty("consentRequired", out var refused) || refused.ValueKind != JsonValueKind.Array)
            return result;
        var required = refused.Deserialize<List<AiConsentRequirement>>(JsonSerializerOptions.Web) ?? [];
        if (required.Count == 0) return result;

        var wanted = string.Join(" and ", required.Select(r =>
            $"{(r.Access == "write" ? "record" : "see")} {AxisWords(r.Axis)}"));
        result.Content.Add(new TextContentBlock
        {
            Text = $"[{AiErrorCodes.ConsentRequired}] The assistant needs permission to {wanted}. "
                + $"The person can allow it in Mizan under Settings, Assistant: {links.AppUrl}/profile/settings#assistant",
        });
        return result;
    }

    private static string AxisWords(string axis) => axis switch
    {
        "training" => "their workouts",
        "body" => "their body measurements",
        _ => "their meals and meal plans",
    };

    [McpServerTool(Name = "list_ai_threads", ReadOnly = true, Idempotent = true)]
    [Description("Your assistant conversations, most recent first.")]
    public Task<string> ListThreads(int take = 30, CancellationToken ct = default) =>
        _api.GetAsync($"/api/Ai/threads?take={Math.Clamp(take, 1, 100)}", ct);

    [McpServerTool(Name = "get_ai_thread", ReadOnly = true, Idempotent = true)]
    [Description("One conversation with its messages.")]
    public Task<string> GetThread(string id, CancellationToken ct = default) =>
        _api.GetAsync($"/api/Ai/threads/{ToolArguments.ParseId(id, "id")}", ct);

    [McpServerTool(Name = "delete_ai_thread", Destructive = true)]
    [Description("Deletes a conversation and its messages. Not recoverable.")]
    public Task<string> DeleteThread(string id, CancellationToken ct = default) =>
        _api.DeleteAsync($"/api/Ai/threads/{ToolArguments.ParseId(id, "id")}", ct);

    // ---- Suggestions ------------------------------------------------------

    [McpServerTool(Name = "suggest_meals")]
    [Description(
        "Proposals for the rest of today against your remaining macros. "
        + "Not read-only: it costs an allowance and writes a usage row.")]
    public Task<string> SuggestMeals(CancellationToken ct = default) =>
        _api.PostAsync("/api/Ai/suggestions", null, ct);
}
