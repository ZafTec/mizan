using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetMyHouseholdsQuery(Guid UserId) : IRequest<GetMyHouseholdsResult>;

public record MyHouseholdSummary(
    Guid Id,
    string Name,
    string MyRole,
    int MemberCount,
    DateTime JoinedAt,
    bool IsActive
);

public record MyHouseholdInvitationSummary(
    Guid Id,
    Guid HouseholdId,
    string HouseholdName,
    string Role,
    string InvitedByName,
    DateTime CreatedAt,
    DateTime ExpiresAt
);

public record GetMyHouseholdsResult(
    List<MyHouseholdSummary> Households,
    List<MyHouseholdInvitationSummary> PendingInvitations,
    Guid? ActiveHouseholdId
);

public class GetMyHouseholdsQueryHandler : IRequestHandler<GetMyHouseholdsQuery, GetMyHouseholdsResult>
{
    private readonly IMizanDbContext _context;

    public GetMyHouseholdsQueryHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<GetMyHouseholdsResult> Handle(GetMyHouseholdsQuery request, CancellationToken cancellationToken)
    {
        var pref = await _context.UserHouseholdPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);
        var activeId = pref?.ActiveHouseholdId;

        var memberships = await _context.HouseholdMembers
            .AsNoTracking()
            .Where(m => m.UserId == request.UserId)
            .Join(_context.Households.AsNoTracking(),
                m => m.HouseholdId,
                h => h.Id,
                (m, h) => new { Membership = m, Household = h })
            .ToListAsync(cancellationToken);

        var householdIds = memberships.Select(x => x.Household.Id).ToList();
        var memberCounts = await _context.HouseholdMembers
            .AsNoTracking()
            .Where(m => householdIds.Contains(m.HouseholdId))
            .GroupBy(m => m.HouseholdId)
            .Select(g => new { HouseholdId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.HouseholdId, g => g.Count, cancellationToken);

        var summaries = memberships
            .Select(x => new MyHouseholdSummary(
                x.Household.Id,
                x.Household.Name,
                x.Membership.Role,
                memberCounts.TryGetValue(x.Household.Id, out var c) ? c : 0,
                x.Membership.JoinedAt,
                activeId == x.Household.Id))
            .OrderByDescending(h => h.IsActive)
            .ThenBy(h => h.Name)
            .ToList();

        // Fall back to first household if no active preference and at least one membership.
        Guid? effectiveActive = activeId;
        if (!effectiveActive.HasValue && summaries.Count > 0)
        {
            effectiveActive = summaries[0].Id;
        }

        var invitations = await _context.HouseholdInvitations
            .AsNoTracking()
            .Where(i => i.InvitedUserId == request.UserId && i.Status == "pending" && i.ExpiresAt > DateTime.UtcNow)
            .Join(_context.Households.AsNoTracking(),
                i => i.HouseholdId,
                h => h.Id,
                (i, h) => new { Invite = i, HouseholdName = h.Name })
            .Join(_context.Users.AsNoTracking(),
                x => x.Invite.InvitedByUserId,
                u => u.Id,
                (x, u) => new MyHouseholdInvitationSummary(
                    x.Invite.Id,
                    x.Invite.HouseholdId,
                    x.HouseholdName,
                    x.Invite.Role,
                    u.Name ?? u.Email,
                    x.Invite.CreatedAt,
                    x.Invite.ExpiresAt))
            .ToListAsync(cancellationToken);

        return new GetMyHouseholdsResult(summaries, invitations, effectiveActive);
    }
}
