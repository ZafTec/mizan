using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Features;

/// <summary>
/// The household a user is acting in: the one they chose, while they still
/// belong to it, else the first of theirs by name, which is what the household
/// switcher shows as active. Null for someone in no household. Feature flags
/// and allowance overrides are decided for this household.
/// </summary>
public sealed class ActiveHousehold
{
    private readonly IMizanDbContext _context;

    public ActiveHousehold(IMizanDbContext context) => _context = context;

    public async Task<Guid?> ForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var memberships = await _context.HouseholdMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new { m.HouseholdId, m.Household.Name })
            .ToListAsync(cancellationToken);
        if (memberships.Count == 0) return null;

        var preferred = await _context.UserHouseholdPreferences.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.ActiveHouseholdId)
            .FirstOrDefaultAsync(cancellationToken);

        if (preferred is { } id && memberships.Any(m => m.HouseholdId == id)) return id;
        return memberships.OrderBy(m => m.Name, StringComparer.Ordinal).ThenBy(m => m.HouseholdId).First().HouseholdId;
    }
}
