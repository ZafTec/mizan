using System.ComponentModel;
using Mizan.Mcp.Server.Services;
using ModelContextProtocol.Server;

namespace Mizan.Mcp.Server.Tools;

[McpServerToolType]
public sealed class HouseholdTools
{
    private readonly IBackendApiClient _api;

    public HouseholdTools(IBackendApiClient api) => _api = api;

    [McpServerTool(Name = "list_my_households", ReadOnly = true, Idempotent = true)]
    [Description("List households I'm a member of, my pending invitations, and my active household.")]
    public async Task<string> ListMine(CancellationToken ct = default)
    {
        return await _api.GetAsync("/api/Households/mine", ct);
    }

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

    [McpServerTool(Name = "switch_household")]
    [Description("Set the active household for the current user. Pass null to clear.")]
    public async Task<string> SwitchHousehold(
        [Description("Household UUID, or null to clear")] string? householdId = null,
        CancellationToken ct = default)
    {
        return await _api.PutAsync("/api/Households/active", new { householdId }, ct);
    }

    [McpServerTool(Name = "invite_to_household")]
    [Description("Invite a registered user to a household by email. Role: 'admin' or 'member' (default member).")]
    public async Task<string> Invite(
        [Description("Household UUID")] string householdId,
        [Description("Email of the user to invite (must already be registered)")] string email,
        [Description("Role to grant on acceptance: admin | member")] string role = "member",
        CancellationToken ct = default)
    {
        return await _api.PostAsync($"/api/Households/{householdId}/invitations", new { email, role }, ct);
    }

    [McpServerTool(Name = "accept_household_invitation")]
    [Description("Accept a pending household invitation. Invitee only.")]
    public async Task<string> Accept(
        [Description("Invitation UUID from list_my_households")] string invitationId,
        CancellationToken ct = default)
    {
        return await _api.PostAsync($"/api/Households/invitations/{invitationId}/respond", new { action = "accept" }, ct);
    }

    [McpServerTool(Name = "decline_household_invitation")]
    [Description("Decline a pending household invitation.")]
    public async Task<string> Decline(
        [Description("Invitation UUID")] string invitationId,
        CancellationToken ct = default)
    {
        return await _api.PostAsync($"/api/Households/invitations/{invitationId}/respond", new { action = "decline" }, ct);
    }

    [McpServerTool(Name = "revoke_household_invitation")]
    [Description("Revoke a pending invitation you sent. Inviter or household admin only.")]
    public async Task<string> Revoke(
        [Description("Invitation UUID")] string invitationId,
        CancellationToken ct = default)
    {
        return await _api.PostAsync($"/api/Households/invitations/{invitationId}/respond", new { action = "revoke" }, ct);
    }

    [McpServerTool(Name = "leave_household", Destructive = true)]
    [Description("Leave a household. Last admin cannot leave; promote someone first.")]
    public async Task<string> Leave(
        [Description("Household UUID")] string householdId,
        CancellationToken ct = default)
    {
        return await _api.PostAsync($"/api/Households/{householdId}/leave", new { }, ct);
    }

    [McpServerTool(Name = "remove_household_member", Destructive = true)]
    [Description("Remove a member from a household. Household admin only.")]
    public async Task<string> RemoveMember(
        [Description("Household UUID")] string householdId,
        [Description("User UUID of the member to remove")] string userId,
        CancellationToken ct = default)
    {
        return await _api.DeleteAsync($"/api/Households/{householdId}/members/{userId}", ct);
    }
}
