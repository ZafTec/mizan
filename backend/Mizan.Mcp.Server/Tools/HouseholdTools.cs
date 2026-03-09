using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class HouseholdTools
{
    private readonly IBackendApiClient _api;

    public HouseholdTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "get_household", ReadOnly = true, Idempotent = true)]
    [Description("Get household details including members.")]
    public async Task<string> GetHousehold(
        [Description("Household UUID")] string id,
        CancellationToken ct = default)
    {
        return await _api.GetAsync($"/api/Households/{id}", ct);
    }

    [McpServerTool(Name = "create_household")]
    [Description("Create a new household for sharing shopping lists and meal plans.")]
    public async Task<string> CreateHousehold(
        [Description("Household name")] string name,
        CancellationToken ct = default)
    {
        return await _api.PostAsync("/api/Households", new { name }, ct);
    }

    [McpServerTool(Name = "add_household_member")]
    [Description("Add a member to a household by their email address.")]
    public async Task<string> AddMember(
        [Description("Household UUID")] string householdId,
        [Description("Email of the user to add")] string email,
        CancellationToken ct = default)
    {
        return await _api.PostAsync($"/api/Households/{householdId}/members", new { email }, ct);
    }
}
