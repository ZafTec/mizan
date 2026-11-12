using System.Text.Json.Nodes;
using Mizan.Application.Commands;
using Mizan.Application.Queries;
using Mizan.Contracts.Mcp;
using Mizan.Domain.Ai;

namespace Mizan.Application.Tools.Catalogue;

/// <summary>
/// Households: the people a person shares plans and shopping lists with. A connection limited to
/// chosen households sees only those; the commands hold that, not the tools.
/// </summary>
public static class HouseholdTools
{
    public static IReadOnlyList<TaskTool> All { get; } =
    [
        new TaskTool
        {
            Name = "get_household",
            Title = "Households",
            Description = "The households the person belongs to, their pending invitations and the active household; one household with its members; "
                + "or the invitations a household has sent.",
            Kind = ToolKind.Read,
            ModeArgument = "view",
            DefaultMode = "mine",
            Properties = new JsonObject
            {
                ["householdId"] = ToolSchema.Id("household, invitations: the household."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "mine",
                    Summary = "The person's households, the invitations waiting for them, and the active household.",
                    Area = McpScopes.PlanningRead,
                    Requests = [typeof(GetMyHouseholdsQuery)],
                    Replaces = ["list_my_households"],
                    Build = (_, context) => new GetMyHouseholdsQuery(context.UserId),
                },
                new ToolMode
                {
                    Name = "household",
                    Summary = "One household, householdId, with its members.",
                    Area = McpScopes.PlanningRead,
                    Requests = [typeof(GetHouseholdQuery)],
                    Replaces = ["get_household"],
                    Build = (args, _) => new GetHouseholdQuery(args.RequiredGuid("householdId")),
                    NotFound = "There is no such household, or the person is not in it.",
                },
                new ToolMode
                {
                    Name = "invitations",
                    Summary = "The invitations householdId has sent, with each one's status. Only for someone who manages its members.",
                    Area = McpScopes.PlanningRead,
                    Requests = [typeof(GetHouseholdInvitationsQuery)],
                    Replaces = ["list_household_invitations"],
                    Build = (args, context) => new GetHouseholdInvitationsQuery(args.RequiredGuid("householdId"), context.UserId),
                    NotFound = "There is no such household that the person manages.",
                },
            ],
        },
        new TaskTool
        {
            Name = "manage_household",
            Title = "Manage households",
            Description = "Create a household, choose the active one, invite a registered person by email, answer an invitation, or revoke one you sent. "
                + "Leaving a household or removing a member is delete, kind household_membership.",
            Kind = ToolKind.Write,
            Additive = true,
            ModeArgument = "action",
            Properties = new JsonObject
            {
                ["name"] = ToolSchema.String("create: the household's name."),
                ["householdId"] = ToolSchema.Id("set_active: the household, or leave it out to clear. invite: the household."),
                ["email"] = ToolSchema.String("invite: the email of a registered person."),
                ["role"] = ToolSchema.Enum("invite: the role they get on accepting, member by default.", ["member", "admin"]),
                ["invitationId"] = ToolSchema.Id("accept, decline, revoke: the invitation, from get_household."),
            },
            Modes =
            [
                new ToolMode
                {
                    Name = "create",
                    Summary = "A household called name, to share plans and shopping with the people the person cooks for.",
                    Area = McpScopes.PlanningWrite,
                    Surfaces = ToolSurface.Both,
                    Axis = DataAxis.Nutrition,
                    Requests = [typeof(CreateHouseholdCommand)],
                    Replaces = ["create_household"],
                    Build = (args, context) => new CreateHouseholdCommand(args.RequiredString("name"), context.UserId),
                    Shape = result => new { householdId = result },
                    Describe = _ => "Created your household.",
                },
                new ToolMode
                {
                    Name = "set_active",
                    Summary = "Make householdId the active household, or clear it.",
                    Area = McpScopes.PlanningWrite,
                    Requests = [typeof(SetActiveHouseholdCommand)],
                    Replaces = ["switch_household"],
                    Build = (args, context) => new SetActiveHouseholdCommand(context.UserId, args.OptionalGuid("householdId")),
                },
                new ToolMode
                {
                    Name = "invite",
                    Summary = "Invite email to householdId as role.",
                    Area = McpScopes.PlanningWrite,
                    Requests = [typeof(InviteHouseholdMemberCommand)],
                    Replaces = ["invite_to_household"],
                    Build = (args, context) => new InviteHouseholdMemberCommand(
                        args.RequiredGuid("householdId"),
                        args.RequiredString("email"),
                        args.OptionalString("role") is { Length: > 0 } role ? role : "member",
                        context.UserId),
                },
                Respond("accept", "Accept invitationId, sent to the person.", "accept_household_invitation"),
                Respond("decline", "Decline invitationId, sent to the person.", "decline_household_invitation"),
                Respond("revoke", "Revoke invitationId, which the person sent or may manage.", "revoke_household_invitation"),
            ],
        },
    ];

    public static IReadOnlyList<ToolMode> DeleteKinds { get; } =
    [
        new ToolMode
        {
            Name = "household_membership",
            Summary = "Leave householdId, or remove memberId from it. Previews first: the household, who leaves and what they lose access to.",
            Area = McpScopes.PlanningWrite,
            Requests = [typeof(LeaveHouseholdCommand), typeof(RemoveHouseholdMemberCommand)],
            Replaces = ["leave_household", "remove_household_member"],
            Build = (args, context) => args.OptionalGuid("memberId") is { } memberId && memberId != context.UserId
                ? (MediatR.IBaseRequest)new RemoveHouseholdMemberCommand(args.RequiredGuid("householdId"), memberId, context.UserId)
                : new LeaveHouseholdCommand(args.RequiredGuid("householdId"), context.UserId),
            Confirm = new ToolConfirmStep(
                typeof(GetHouseholdMembershipDeletePreviewQuery),
                (args, _) => new GetHouseholdMembershipDeletePreviewQuery(args.RequiredGuid("householdId"), args.OptionalGuid("memberId")),
                result => MembershipFacts(result as HouseholdMembershipDeletePreview)),
        },
    ];

    private static ToolMode Respond(string action, string summary, string replaces) => new()
    {
        Name = action,
        Summary = summary,
        Area = McpScopes.PlanningWrite,
        Requests = [typeof(RespondToHouseholdInvitationCommand)],
        Replaces = [replaces],
        Build = (args, context) => new RespondToHouseholdInvitationCommand(args.RequiredGuid("invitationId"), context.UserId, action),
    };

    /// <summary>Who leaves and what stops being theirs to see. Names and counts only, so the same state gives the same facts.</summary>
    private static JsonNode MembershipFacts(HouseholdMembershipDeletePreview? preview)
    {
        if (preview is null) return new JsonObject();
        return new JsonObject
        {
            ["household"] = preview.Household,
            ["householdId"] = preview.HouseholdId.ToString(),
            ["leaving"] = preview.Yourself ? "you" : preview.Member ?? preview.MemberId.ToString(),
            ["memberId"] = preview.MemberId.ToString(),
            ["role"] = preview.Role,
            ["membersLeft"] = preview.MembersLeft,
            ["loseAccessTo"] = new JsonObject
            {
                ["householdMealPlans"] = preview.SharedMealPlans,
                ["householdShoppingLists"] = preview.SharedShoppingLists,
                ["householdRecipes"] = preview.SharedRecipes,
            },
        };
    }
}
