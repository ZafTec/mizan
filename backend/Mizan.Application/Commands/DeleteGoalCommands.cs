using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

/// <summary>
/// Deletes one of the person's goals with its check-ins. Deleting the active goal leaves no goal active: an
/// older goal is history and is not brought back, so the person sets a new one when they want targets again.
/// </summary>
public record DeleteUserGoalCommand(Guid Id) : IRequest<bool>;

public class DeleteUserGoalHandler : IRequestHandler<DeleteUserGoalCommand, bool>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly HybridCache _cache;

    public DeleteUserGoalHandler(IMizanDbContext context, ICurrentUserService currentUser, HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<bool> Handle(DeleteUserGoalCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var goal = await _context.UserGoals.FirstOrDefaultAsync(g => g.Id == request.Id && g.UserId == userId, cancellationToken);
        if (goal is null) return false;

        var progress = await _context.GoalProgress.Where(p => p.UserGoalId == goal.Id).ToListAsync(cancellationToken);
        _context.GoalProgress.RemoveRange(progress);
        _context.UserGoals.Remove(goal);
        await _context.SaveChangesAsync(cancellationToken);

        // The goal's targets feed the daily totals' Target* fields.
        await _cache.RemoveByTagAsync(CacheTags.Nutrition(userId), cancellationToken);
        return true;
    }
}

/// <summary>Deletes one of the person's goal check-ins.</summary>
public record DeleteGoalProgressCommand(Guid Id) : IRequest<bool>;

public class DeleteGoalProgressHandler : IRequestHandler<DeleteGoalProgressCommand, bool>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteGoalProgressHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(DeleteGoalProgressCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var entry = await _context.GoalProgress.FirstOrDefaultAsync(p => p.Id == request.Id && p.UserId == userId, cancellationToken);
        if (entry is null) return false;

        _context.GoalProgress.Remove(entry);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
