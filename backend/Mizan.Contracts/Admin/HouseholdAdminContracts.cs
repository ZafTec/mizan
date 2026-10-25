namespace Mizan.Contracts.Admin;

/// <summary>A member as the administrator sees them on GET /api/Households/admin/{id}.</summary>
public record AdminHouseholdMemberDto(
    Guid UserId,
    string? Name,
    string Email,
    string Role,
    DateTime JoinedAt,
    bool IsOwner);

public record AdminHouseholdInvitationDto(
    Guid Id,
    string? InvitedName,
    string InvitedEmail,
    string Role,
    DateTime ExpiresAt);

/// <summary>
/// One household for support: its members with roles and join dates, who owns
/// it, and how many plans and lists it holds. Support notes are read
/// separately, from /api/admin/support-notes.
/// </summary>
public record AdminHouseholdDetailDto(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    Guid CreatedBy,
    Guid? OwnerUserId,
    string? OwnerName,
    string? OwnerEmail,
    IReadOnlyList<AdminHouseholdMemberDto> Members,
    IReadOnlyList<AdminHouseholdInvitationDto> PendingInvitations,
    int MealPlanCount,
    int ShoppingListCount);
