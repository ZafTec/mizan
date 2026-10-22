using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Interfaces;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>The person declines a draft, a coach's or a generated one: it is archived and a coach is told.</summary>
public record DeclinePlanCommand(Guid Id) : IRequest<Unit>;

public class DeclinePlanCommandHandler : IRequestHandler<DeclinePlanCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly PlanAccess _access;
    private readonly INotificationWriter _notifications;
    private readonly HybridCache _cache;

    public DeclinePlanCommandHandler(IMizanDbContext context, PlanAccess access, INotificationWriter notifications, HybridCache cache)
    {
        _context = context;
        _access = access;
        _notifications = notifications;
        _cache = cache;
    }

    public async Task<Unit> Handle(DeclinePlanCommand request, CancellationToken cancellationToken)
    {
        var userId = _access.RequireUserId();
        var plan = await _access.LoadReadableAsync(request.Id, cancellationToken);
        DraftDecision.EnsureClientCanDecide(plan, userId);

        plan.Status = PlanStatus.Archived;
        plan.UpdatedAt = DateTime.UtcNow;
        await DraftDecision.TellCoachAsync(_context, _notifications, plan, userId, accepted: false, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await PlanAccess.InvalidateAsync(_cache, plan, cancellationToken);
        return Unit.Value;
    }
}
