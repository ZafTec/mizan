using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Admin;

namespace Mizan.Application.Queries;

/// <summary>
/// Admin only. One household for support: members with their roles and join
/// dates, the owner by the same rule deletion uses (<see cref="HouseholdOwnership"/>),
/// pending invitations, and how many plans and lists it holds.
/// </summary>
public record GetAdminHouseholdQuery(Guid Id) : IRequest<AdminHouseholdDetailDto>;

public class GetAdminHouseholdQueryHandler : IRequestHandler<GetAdminHouseholdQuery, AdminHouseholdDetailDto>
{
    private readonly IMizanDbContext _context;

    public GetAdminHouseholdQueryHandler(IMizanDbContext context) => _context = context;

    public async Task<AdminHouseholdDetailDto> Handle(GetAdminHouseholdQuery request, CancellationToken cancellationToken)
    {
        var household = await _context.Households.AsNoTracking().FirstOrDefaultAsync(h => h.Id == request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("Household", request.Id);

        var members = await _context.HouseholdMembers.AsNoTracking()
            .Where(m => m.HouseholdId == household.Id)
            .Include(m => m.User)
            .OrderBy(m => m.JoinedAt)
            .ToListAsync(cancellationToken);
        var ownerId = HouseholdOwnership.OwnerId(household, members);
        var owner = members.FirstOrDefault(m => m.UserId == ownerId)?.User;

        var now = DateTime.UtcNow;
        var invitations = await _context.HouseholdInvitations.AsNoTracking()
            .Where(i => i.HouseholdId == household.Id && i.Status == "pending" && i.ExpiresAt > now)
            .OrderBy(i => i.CreatedAt)
            .Select(i => new AdminHouseholdInvitationDto(i.Id, i.InvitedUser.Name, i.InvitedUser.Email, i.Role, i.ExpiresAt))
            .ToListAsync(cancellationToken);

        return new AdminHouseholdDetailDto(
            household.Id,
            household.Name,
            household.CreatedAt,
            household.CreatedBy,
            ownerId,
            owner?.Name,
            owner?.Email,
            members.Select(m => new AdminHouseholdMemberDto(m.UserId, m.User.Name, m.User.Email, m.Role, m.JoinedAt, m.UserId == ownerId)).ToList(),
            invitations,
            await _context.MealPlans.CountAsync(p => p.HouseholdId == household.Id, cancellationToken),
            await _context.ShoppingLists.CountAsync(l => l.HouseholdId == household.Id, cancellationToken));
    }
}
