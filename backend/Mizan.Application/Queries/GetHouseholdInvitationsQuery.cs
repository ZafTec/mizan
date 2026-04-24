using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

// Lists pending invitations sent from a household, shown to admins in the
// /settings/household UI so they can see who hasn't responded yet and revoke.
public record GetHouseholdInvitationsQuery(Guid HouseholdId, Guid RequestingUserId) : IRequest<List<HouseholdInvitationAdminDto>?>;

public record HouseholdInvitationAdminDto(
    Guid Id,
    Guid HouseholdId,
    string InvitedEmail,
    string? InvitedName,
    string Role,
    string Status,
    DateTime CreatedAt,
    DateTime ExpiresAt
);

public class GetHouseholdInvitationsQueryHandler : IRequestHandler<GetHouseholdInvitationsQuery, List<HouseholdInvitationAdminDto>?>
{
    private readonly IMizanDbContext _context;

    public GetHouseholdInvitationsQueryHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<List<HouseholdInvitationAdminDto>?> Handle(GetHouseholdInvitationsQuery request, CancellationToken cancellationToken)
    {
        var isMember = await _context.HouseholdMembers
            .AsNoTracking()
            .AnyAsync(m => m.HouseholdId == request.HouseholdId && m.UserId == request.RequestingUserId, cancellationToken);
        if (!isMember)
        {
            return null;
        }

        return await _context.HouseholdInvitations
            .AsNoTracking()
            .Where(i => i.HouseholdId == request.HouseholdId && i.Status == "pending")
            .Join(_context.Users.AsNoTracking(),
                i => i.InvitedUserId,
                u => u.Id,
                (i, u) => new HouseholdInvitationAdminDto(
                    i.Id,
                    i.HouseholdId,
                    u.Email,
                    u.Name,
                    i.Role,
                    i.Status,
                    i.CreatedAt,
                    i.ExpiresAt))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
