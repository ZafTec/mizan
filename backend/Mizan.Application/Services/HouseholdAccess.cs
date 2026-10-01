using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Services;

public sealed class HouseholdAccess : IHouseholdAccess
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public HouseholdAccess(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<bool> CanAccessAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserId is not { } userId) return false;
        if (_currentUser.Grant is { } grant && !grant.AllowsHousehold(householdId)) return false;

        return await _context.HouseholdMembers.AnyAsync(
            m => m.HouseholdId == householdId && m.UserId == userId, cancellationToken);
    }

    public async Task<bool> CanAccessRecordAsync(Guid ownerId, Guid? householdId, CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserId is not { } userId) return false;
        if (householdId is { } household && _currentUser.Grant is { } grant && !grant.AllowsHousehold(household)) return false;
        if (ownerId == userId) return true;
        return householdId is { } id && await CanAccessAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> AccessibleIdsAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserId is not { } userId) return [];
        var grant = _currentUser.Grant;
        if (grant is { HouseholdMode: not ("all" or "selected") }) return [];

        var query = _context.HouseholdMembers.Where(m => m.UserId == userId);
        if (grant is { HouseholdMode: "selected" })
        {
            var allowed = grant.HouseholdIds.ToList();
            query = query.Where(m => allowed.Contains(m.HouseholdId));
        }

        return await query.Select(m => m.HouseholdId).ToListAsync(cancellationToken);
    }
}
