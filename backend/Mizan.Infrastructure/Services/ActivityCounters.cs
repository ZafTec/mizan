using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;

namespace Mizan.Infrastructure.Services;

/// <summary>
/// The read side, plus a manual adjustment for backfills and admin repair.
/// Ordinary writes go through <see cref="ActivityCounterInterceptor"/>, which
/// cannot be forgotten.
/// </summary>
public class ActivityCounters : IActivityCounters
{
    private readonly MizanDbContext _context;

    public ActivityCounters(MizanDbContext context) => _context = context;

    public Task AdjustAsync(
        Guid userId, ActivityCounter counter, int delta = 1, CancellationToken cancellationToken = default) =>
        _context.Database.ExecuteSqlRawAsync(
            ActivityCounterSql.Adjust(counter), [userId, delta], cancellationToken);

    public async Task<UserActivityCounters> GetAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        await _context.UserActivityCounters.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken)
        ?? new UserActivityCounters { UserId = userId };
}
