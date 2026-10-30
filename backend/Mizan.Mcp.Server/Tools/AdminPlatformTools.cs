using System.ComponentModel;
using Mizan.Contracts.Admin;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

/// <summary>
/// Feature flags, AI allowance overrides and platform usage, as tools over the
/// same admin endpoints the console uses (docs/ADMIN.md). The API decides and
/// audits; nothing here knows a rule.
/// </summary>
[McpServerToolType]
public sealed class AdminPlatformTools
{
    private readonly IBackendApiClient _api;

    public AdminPlatformTools(IBackendApiClient api) => _api = api;

    // ---- Feature flags ----------------------------------------------------

    [McpServerTool(Name = "admin_list_feature_flags", ReadOnly = true, Idempotent = true)]
    [Description("Admin only. Every feature flag with its default and how many households override it.")]
    public Task<string> ListFeatureFlags(CancellationToken ct = default) =>
        _api.GetAsync("/api/admin/feature-flags", ct);

    [McpServerTool(Name = "admin_set_feature_flag", Destructive = true, Idempotent = true)]
    [Description(
        "Admin only. Sets a flag's default for every household without an override. "
        + "key is one of planning.generate, prices, telegram.bot, ai.assistant, social.feed.")]
    public Task<string> SetFeatureFlag(string key, bool enabled, CancellationToken ct = default) =>
        _api.PutAsync($"/api/admin/feature-flags/{Uri.EscapeDataString(key)}", new SetFeatureFlagRequest(enabled), ct);

    [McpServerTool(Name = "admin_list_feature_flag_overrides", ReadOnly = true, Idempotent = true)]
    [Description("Admin only. The households that override one feature flag, and whether each has it on or off.")]
    public Task<string> ListFeatureFlagOverrides(string key, CancellationToken ct = default) =>
        _api.GetAsync($"/api/admin/feature-flags/{Uri.EscapeDataString(key)}/overrides", ct);

    [McpServerTool(Name = "admin_set_feature_flag_override", Destructive = true, Idempotent = true)]
    [Description("Admin only. Turns a flag on or off for one household, whatever the default says.")]
    public Task<string> SetFeatureFlagOverride(string key, string householdId, bool enabled, CancellationToken ct = default) =>
        _api.PutAsync(
            $"/api/admin/feature-flags/{Uri.EscapeDataString(key)}/overrides/{ToolArguments.ParseId(householdId, "householdId")}",
            new SetFeatureFlagRequest(enabled),
            ct);

    [McpServerTool(Name = "admin_clear_feature_flag_override", Destructive = true, Idempotent = true)]
    [Description("Admin only. Puts one household back on the flag's default.")]
    public Task<string> ClearFeatureFlagOverride(string key, string householdId, CancellationToken ct = default) =>
        _api.DeleteAsync(
            $"/api/admin/feature-flags/{Uri.EscapeDataString(key)}/overrides/{ToolArguments.ParseId(householdId, "householdId")}",
            ct);

    // ---- Usage limits -----------------------------------------------------

    [McpServerTool(Name = "admin_get_usage_limit", ReadOnly = true, Idempotent = true)]
    [Description("Admin only. The AI allowance in force for one plan, household or user, and where each value comes from. scope is Plan, Household or User.")]
    public Task<string> GetUsageLimit(string scope, [Description("The plan, household or user UUID")] string subjectId, CancellationToken ct = default) =>
        _api.GetAsync($"/api/admin/usage-limits/{Uri.EscapeDataString(scope)}/{ToolArguments.ParseId(subjectId, "subjectId")}", ct);

    [McpServerTool(Name = "admin_list_usage_limits", ReadOnly = true, Idempotent = true)]
    [Description(
        "Admin only. AI allowance overrides with the limits in force and where each value comes from. "
        + "scope is Plan (every billing plan), Household or User (those with an override).")]
    public Task<string> ListUsageLimits(string scope = "Plan", CancellationToken ct = default) =>
        _api.GetAsync($"/api/admin/usage-limits?scope={Uri.EscapeDataString(scope)}", ct);

    [McpServerTool(Name = "admin_set_usage_limit", Destructive = true, Idempotent = true)]
    [Description(
        "Admin only. Creates or replaces an allowance override. scope is Plan, Household or User and subjectId the "
        + "plan, household or user id. line is Personal, Planning, Onboarding, TrainerClient or Eval; omit it for "
        + "every line. An omitted cap inherits: user over household over plan over the tier default. Costs are in "
        + "millionths of the currency unit.")]
    public Task<string> SetUsageLimit(
        string scope,
        string subjectId,
        string? line = null,
        int? dailyRequests = null,
        long? dailyTokens = null,
        long? dailyCostMicros = null,
        long? monthlyCostMicros = null,
        string? note = null,
        CancellationToken ct = default) =>
        _api.PutAsync(
            $"/api/admin/usage-limits/{Uri.EscapeDataString(scope)}/{ToolArguments.ParseId(subjectId, "subjectId")}",
            new SetUsageLimitRequest(line, dailyRequests, dailyTokens, dailyCostMicros, monthlyCostMicros, note),
            ct);

    [McpServerTool(Name = "admin_clear_usage_limit", Destructive = true, Idempotent = true)]
    [Description("Admin only. Removes one allowance override so the subject inherits again. Omit line for the every-line row.")]
    public Task<string> ClearUsageLimit(string scope, string subjectId, string? line = null, CancellationToken ct = default)
    {
        var query = new QueryString().Add("line", line);
        return _api.DeleteAsync(
            $"/api/admin/usage-limits/{Uri.EscapeDataString(scope)}/{ToolArguments.ParseId(subjectId, "subjectId")}{query}",
            ct);
    }

    // ---- Usage ------------------------------------------------------------

    [McpServerTool(Name = "admin_get_platform_usage", ReadOnly = true, Idempotent = true)]
    [Description(
        "Admin only. AI requests, tokens, cost and refusals from the usage ledger, grouped by household, user, plan "
        + "or feature. from and to are inclusive UTC dates, YYYY-MM-DD; they default to this month. householdId and "
        + "userId narrow the rows.")]
    public Task<string> PlatformUsage(
        [Description("household, user, plan or feature")] string groupBy = "household",
        [Description("Inclusive start, YYYY-MM-DD")] string? from = null,
        [Description("Inclusive end, YYYY-MM-DD")] string? to = null,
        string? householdId = null,
        string? userId = null,
        CancellationToken ct = default)
    {
        var query = new QueryString()
            .Add("groupBy", groupBy)
            .Add("from", ToolArguments.ParseOptionalDate(from, "from")?.ToString("yyyy-MM-dd"))
            .Add("to", ToolArguments.ParseOptionalDate(to, "to")?.ToString("yyyy-MM-dd"))
            .Add("householdId", ToolArguments.ParseOptionalId(householdId, "householdId")?.ToString())
            .Add("userId", ToolArguments.ParseOptionalId(userId, "userId")?.ToString());
        return _api.GetAsync($"/api/admin/usage{query}", ct);
    }
}
