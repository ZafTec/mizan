using System.ComponentModel;
using System.Text.Json;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

/// <summary>
/// The admin surface, as tools.
///
/// Everything here is behind the admin API key. The rule for what belongs is
/// the same one the console follows: operational access, not super-user access
/// over personal data (docs/REFOCUS.md §11). So an admin can end a
/// trainer-client relationship but not edit which axes the client shares, and
/// can read the audit log but not write to it.
/// </summary>
[McpServerToolType]
public sealed class AdminTools
{
    private readonly IBackendApiClient _api;

    public AdminTools(IBackendApiClient api) => _api = api;

    // ---- Moderation -------------------------------------------------------

    [McpServerTool(Name = "admin_get_social_analytics", ReadOnly = true, Idempotent = true)]
    [Description("Admin only. Feed, follow and report counts.")]
    public Task<string> SocialAnalytics(CancellationToken ct = default) =>
        _api.GetAsync("/api/admin/social/analytics", ct);

    [McpServerTool(Name = "admin_list_content_reports", ReadOnly = true, Idempotent = true)]
    [Description("Admin only. Reported feed content awaiting a decision.")]
    public Task<string> Reports(string status = "Open", int page = 1, CancellationToken ct = default) =>
        _api.GetAsync($"/api/admin/social/reports?status={Uri.EscapeDataString(status)}&page={page}", ct);

    [McpServerTool(Name = "admin_resolve_content_report", Destructive = true)]
    [Description("Admin only. action is dismiss or delete.")]
    public Task<string> ResolveReport(string id, string action, string? note = null, CancellationToken ct = default) =>
        _api.PostAsync($"/api/admin/social/reports/{ToolArguments.ParseId(id, "id")}/resolve", new { action, note }, ct);

    [McpServerTool(Name = "admin_promote_exercise", Destructive = true)]
    [Description("Admin only. Promotes a user exercise into the global catalog.")]
    public Task<string> PromoteExercise(string id, CancellationToken ct = default) =>
        _api.PostAsync($"/api/Exercises/{ToolArguments.ParseId(id, "id")}/promote", null, ct);

    [McpServerTool(Name = "admin_save_builtin_workout_template")]
    [Description("Admin only. Create or update a built-in template using the full JSON contract.")]
    public Task<string> SaveTemplate(string body, string? id = null, CancellationToken ct = default) =>
        id is null
            ? _api.PostAsync("/api/WorkoutTemplates", JsonSerializer.Deserialize<object>(body), ct)
            : _api.PutAsync($"/api/WorkoutTemplates/{ToolArguments.ParseId(id, "id")}", JsonSerializer.Deserialize<object>(body)!, ct);

    // ---- Audit log --------------------------------------------------------

    [McpServerTool(Name = "admin_list_audit_logs", ReadOnly = true, Idempotent = true)]
    [Description(
        "Admin only. Filterable audit log. All filters are optional: action and entityType "
        + "narrow by kind, entityId and search find a specific record or actor, from/to are "
        + "inclusive YYYY-MM-DD bounds.")]
    public Task<string> AuditLogs(
        string? action = null,
        string? entityType = null,
        string? entityId = null,
        string? search = null,
        [Description("Inclusive lower bound, YYYY-MM-DD")] string? from = null,
        [Description("Inclusive upper bound, YYYY-MM-DD")] string? to = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = new QueryString()
            .Add("action", action)
            .Add("entityType", entityType)
            .Add("entityId", entityId)
            .Add("search", search)
            .Add("from", ToolArguments.ParseOptionalDate(from, "from")?.ToString("yyyy-MM-dd"))
            .Add("to", ToolArguments.ParseOptionalDate(to, "to")?.ToString("yyyy-MM-dd"))
            .Add("page", page)
            .Add("pageSize", pageSize);

        return _api.GetAsync($"/api/AuditLogs{query}", ct);
    }

    [McpServerTool(Name = "admin_get_audit_log_facets", ReadOnly = true, Idempotent = true)]
    [Description("Admin only. The distinct actions and entity types present in the log, for filtering.")]
    public Task<string> AuditFacets(CancellationToken ct = default) =>
        _api.GetAsync("/api/AuditLogs/facets", ct);

    // ---- Users ------------------------------------------------------------

    [McpServerTool(Name = "admin_list_users", ReadOnly = true, Idempotent = true)]
    [Description("Admin only. Accounts, with optional search, role and banned filters.")]
    public Task<string> ListUsers(
        string? search = null,
        [Description("user, trainer or admin")] string? role = null,
        bool? banned = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new QueryString()
            .Add("search", search)
            .Add("role", role)
            .Add("banned", banned?.ToString().ToLowerInvariant())
            .Add("page", page)
            .Add("pageSize", pageSize);

        return _api.GetAsync($"/api/admin/users{query}", ct);
    }

    [McpServerTool(Name = "admin_get_user", ReadOnly = true, Idempotent = true)]
    [Description("Admin only. One account with its sessions and status.")]
    public Task<string> GetUser(string id, CancellationToken ct = default) =>
        _api.GetAsync($"/api/admin/users/{ToolArguments.ParseId(id, "id")}", ct);

    [McpServerTool(Name = "admin_update_user", Destructive = true)]
    [Description(
        "Admin only. Partial update - only what you pass changes. role is user, trainer or "
        + "admin. Set banned true with a reason to ban, false to lift it. banExpires is an "
        + "optional ISO 8601 timestamp; omit it for an indefinite ban.")]
    public Task<string> UpdateUser(
        string id,
        string? role = null,
        bool? banned = null,
        string? banReason = null,
        string? banExpires = null,
        bool? emailVerified = null,
        CancellationToken ct = default) =>
        _api.PatchAsync(
            $"/api/admin/users/{ToolArguments.ParseId(id, "id")}",
            new
            {
                role,
                banned,
                banReason,
                banExpires = ToolArguments.ParseOptionalTimestamp(banExpires, "banExpires"),
                emailVerified,
                // Deliberately not exposed. Setting someone's password from a
                // tool call is the kind of thing that wants a human, a browser
                // and a second look (docs/REFOCUS.md §11).
                newPassword = (string?)null,
            },
            ct);

    [McpServerTool(Name = "admin_revoke_user_sessions", Destructive = true)]
    [Description("Admin only. Signs an account out everywhere on its next request.")]
    public Task<string> RevokeSessions(string id, CancellationToken ct = default) =>
        _api.DeleteAsync($"/api/admin/users/{ToolArguments.ParseId(id, "id")}/sessions", ct);

    [McpServerTool(Name = "admin_list_sessions", ReadOnly = true, Idempotent = true)]
    [Description("Admin only. Active sign-ins across the system.")]
    public Task<string> ListSessions(bool activeOnly = true, int page = 1, int pageSize = 50, CancellationToken ct = default) =>
        _api.GetAsync(
            $"/api/admin/sessions{new QueryString().Add("activeOnly", activeOnly.ToString().ToLowerInvariant()).Add("page", page).Add("pageSize", pageSize)}",
            ct);

    // ---- Trainer relationships -------------------------------------------

    [McpServerTool(Name = "admin_list_relationships", ReadOnly = true, Idempotent = true)]
    [Description(
        "Admin only. Trainer-client links with the per-axis grants the client gave. "
        + "search matches either side's name or email.")]
    public Task<string> ListRelationships(
        string? search = null,
        [Description("pending, active, paused or ended")] string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new QueryString()
            .Add("search", search)
            .Add("status", status)
            .Add("page", page)
            .Add("pageSize", pageSize);

        return _api.GetAsync($"/api/Admin/Relationships{query}", ct);
    }

    [McpServerTool(Name = "admin_end_relationship", Destructive = true)]
    [Description(
        "Admin only. Ends a trainer-client relationship, revoking the trainer's access. "
        + "There is deliberately no tool to edit the grants themselves - those belong to the client.")]
    public Task<string> EndRelationship(string id, string? reason = null, CancellationToken ct = default) =>
        _api.PostAsync($"/api/Admin/Relationships/{ToolArguments.ParseId(id, "id")}/end", new { reason }, ct);

    // ---- Background jobs --------------------------------------------------

    [McpServerTool(Name = "admin_list_jobs", ReadOnly = true, Idempotent = true)]
    [Description(
        "Admin only. The background queue. type is email or eval-run; status is Pending, "
        + "Running, Succeeded, Failed or DeadLettered. Dead-lettered rows are the ones worth "
        + "looking at: each is something a user asked for that never happened.")]
    public Task<string> ListJobs(
        string? type = null,
        [Description("Pending, Running, Succeeded, Failed or DeadLettered")] string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new QueryString()
            .Add("type", type)
            .Add("status", status)
            .Add("page", page)
            .Add("pageSize", pageSize);

        return _api.GetAsync($"/api/Admin/Jobs{query}", ct);
    }

    [McpServerTool(Name = "admin_get_job_stats", ReadOnly = true, Idempotent = true)]
    [Description("Admin only. Queue depth by status, and the job types present.")]
    public Task<string> JobStats(CancellationToken ct = default) =>
        _api.GetAsync("/api/Admin/Jobs/stats", ct);

    [McpServerTool(Name = "admin_retry_job")]
    [Description(
        "Admin only. Requeues a failed or dead-lettered job and resets its attempt count. "
        + "Retry after fixing the cause, not instead of finding it.")]
    public Task<string> RetryJob(string id, CancellationToken ct = default) =>
        _api.PostAsync($"/api/Admin/Jobs/{ToolArguments.ParseId(id, "id")}/retry", null, ct);

    [McpServerTool(Name = "admin_delete_job", Destructive = true)]
    [Description("Admin only. Discards a dead-lettered or succeeded job. Pending work cannot be deleted.")]
    public Task<string> DeleteJob(string id, CancellationToken ct = default) =>
        _api.DeleteAsync($"/api/Admin/Jobs/{ToolArguments.ParseId(id, "id")}", ct);

    // ---- Prompt console ---------------------------------------------------

    [McpServerTool(Name = "admin_list_ai_prompts", ReadOnly = true, Idempotent = true)]
    [Description("Admin only. The assistant's prompts and which version of each is live.")]
    public Task<string> ListPrompts(CancellationToken ct = default) =>
        _api.GetAsync("/api/Admin/Ai/Prompts", ct);

    [McpServerTool(Name = "admin_get_ai_prompt", ReadOnly = true, Idempotent = true)]
    [Description(
        "Admin only. One prompt: its hard constraints, its version history, and the "
        + "built-in default. The hard constraints are code and cannot be edited from here "
        + "or anywhere else - a draft only supplies the soft half.")]
    public Task<string> GetPrompt(string key, CancellationToken ct = default) =>
        _api.GetAsync($"/api/Admin/Ai/Prompts/{Uri.EscapeDataString(key)}", ct);

    [McpServerTool(Name = "admin_create_ai_prompt_draft")]
    [Description(
        "Admin only. Branches a new draft. Omit body and softPolicy to start from whatever "
        + "is live, or from the built-in default when nothing is published.")]
    public Task<string> CreateDraft(
        string key,
        string? body = null,
        [Description("JSON object of soft guardrails")] string? softPolicy = null,
        string? notes = null,
        CancellationToken ct = default) =>
        _api.PostAsync(
            $"/api/Admin/Ai/Prompts/{Uri.EscapeDataString(key)}/drafts",
            new { body, softPolicy, notes },
            ct);

    [McpServerTool(Name = "admin_update_ai_prompt_draft")]
    [Description(
        "Admin only. Rewrites a draft. Saving discards whatever the old text proved, so the "
        + "suite has to be run again before it can be published.")]
    public Task<string> UpdateDraft(
        string id,
        string body,
        [Description("JSON object of soft guardrails")] string softPolicy = "{}",
        string? notes = null,
        CancellationToken ct = default) =>
        _api.PutAsync(
            $"/api/Admin/Ai/Prompts/versions/{ToolArguments.ParseId(id, "id")}",
            new { body, softPolicy, notes },
            ct);

    [McpServerTool(Name = "admin_run_ai_prompt_evals")]
    [Description(
        "Admin only. Queues the eval suite against a draft and returns a job id. The suite "
        + "is twenty-odd provider calls and runs in the background - poll "
        + "admin_get_ai_prompt_evals for results.")]
    public Task<string> RunEvals(string id, CancellationToken ct = default) =>
        _api.PostAsync($"/api/Admin/Ai/Prompts/versions/{ToolArguments.ParseId(id, "id")}/evals", null, ct);

    [McpServerTool(Name = "admin_get_ai_prompt_evals", ReadOnly = true, Idempotent = true)]
    [Description(
        "Admin only. The eval matrix for a version: every case, its outcome, the cost, and "
        + "whether the draft is publishable. runStatus says whether a suite is still going - "
        + "results below it are from the previous run until it finishes.")]
    public Task<string> GetEvals(string id, CancellationToken ct = default) =>
        _api.GetAsync($"/api/Admin/Ai/Prompts/versions/{ToolArguments.ParseId(id, "id")}/evals", ct);

    [McpServerTool(Name = "admin_publish_ai_prompt_version", Destructive = true)]
    [Description(
        "Admin only. Makes a version live, archiving whatever it replaces. Publishing an "
        + "archived version is the rollback. Refused while the adversarial cases are unbeaten - "
        + "that gate is in the handler, not only in the console.")]
    public Task<string> PublishVersion(string id, CancellationToken ct = default) =>
        _api.PostAsync($"/api/Admin/Ai/Prompts/versions/{ToolArguments.ParseId(id, "id")}/publish", null, ct);

    [McpServerTool(Name = "admin_get_global_ai_usage", ReadOnly = true, Idempotent = true)]
    [Description(
        "Admin only. Today's provider spend against the global ceilings, by feature. "
        + "This is the number to look at before anyone raises Ai__GlobalDailyTokens.")]
    public Task<string> GlobalAiUsage(CancellationToken ct = default) =>
        _api.GetAsync("/api/Ai/usage/global", ct);
}
