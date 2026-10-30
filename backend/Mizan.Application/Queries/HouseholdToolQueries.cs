using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Planning;

namespace Mizan.Application.Queries;

/// <summary>
/// What leaving a household, or removing a member from it, takes away, for the person to see
/// before they confirm: the household, who leaves, and the shared plans and shopping lists that
/// person no longer reaches. Without <c>MemberId</c> the caller is the one who leaves. Names and
/// counts only, so the same state gives the same preview.
/// </summary>
public sealed record GetHouseholdMembershipDeletePreviewQuery(Guid HouseholdId, Guid? MemberId) : IRequest<HouseholdMembershipDeletePreview>;

public sealed record HouseholdMembershipDeletePreview(
    Guid HouseholdId,
    string Household,
    Guid MemberId,
    string? Member,
    bool Yourself,
    string? Role,
    int MembersLeft,
    int SharedMealPlans,
    int SharedShoppingLists);

public sealed class GetHouseholdMembershipDeletePreviewQueryHandler
    : IRequestHandler<GetHouseholdMembershipDeletePreviewQuery, HouseholdMembershipDeletePreview>
{
    private readonly ISender _sender;
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetHouseholdMembershipDeletePreviewQueryHandler(ISender sender, IMizanDbContext context, ICurrentUserService currentUser)
    {
        _sender = sender;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<HouseholdMembershipDeletePreview> Handle(GetHouseholdMembershipDeletePreviewQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        // Through the household's own read, so only a member who may see it gets a preview.
        var household = await _sender.Send(new GetHouseholdQuery(request.HouseholdId), cancellationToken)
            ?? throw new EntityNotFoundException($"Household {request.HouseholdId} was not found.");
        var memberId = request.MemberId ?? userId;
        var member = household.Members.FirstOrDefault(m => m.UserId == memberId)
            ?? throw new EntityNotFoundException("That person is not a member of this household.");

        // The household's plans and lists the member does not own: what stops being theirs to see.
        var plans = await _context.MealPlans.AsNoTracking()
            .CountAsync(p => p.HouseholdId == household.Id && p.Kind == PlanKind.Personal && p.UserId != memberId, cancellationToken);
        var lists = await _context.ShoppingLists.AsNoTracking()
            .CountAsync(l => l.HouseholdId == household.Id && l.UserId != memberId, cancellationToken);

        return new HouseholdMembershipDeletePreview(
            household.Id,
            household.Name,
            memberId,
            member.Name,
            memberId == userId,
            member.Role,
            household.Members.Count - 1,
            plans,
            lists);
    }
}
