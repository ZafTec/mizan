using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetMySubscriptionQuery : IRequest<MySubscriptionDto>;

public record MySubscriptionDto
{
    public string Plan { get; init; } = "free";
    public string Status { get; init; } = "none";
    public bool IsPro { get; init; }
    public bool IsLifetime { get; init; }
    public DateTime? CurrentPeriodEnd { get; init; }
    public DateTime? TrialEndsAt { get; init; }
    public DateTime? CanceledAt { get; init; }
}

public class GetMySubscriptionQueryHandler : IRequestHandler<GetMySubscriptionQuery, MySubscriptionDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IEntitlementService _entitlements;

    public GetMySubscriptionQueryHandler(
        IMizanDbContext context,
        ICurrentUserService currentUser,
        IEntitlementService entitlements)
    {
        _context = context;
        _currentUser = currentUser;
        _entitlements = entitlements;
    }

    public async Task<MySubscriptionDto> Handle(GetMySubscriptionQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        var userId = _currentUser.UserId.Value;
        var entitlement = await _entitlements.GetAsync(userId, cancellationToken);

        var sub = await _context.Subscriptions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new { s.Plan, s.Status, s.IsLifetime, s.CurrentPeriodEnd, s.TrialEndsAt, s.CanceledAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (sub is null)
        {
            return new MySubscriptionDto { Plan = "free", Status = "none", IsPro = entitlement.IsPro };
        }

        return new MySubscriptionDto
        {
            Plan = sub.Plan,
            Status = sub.Status,
            IsPro = entitlement.IsPro,
            IsLifetime = sub.IsLifetime,
            CurrentPeriodEnd = sub.CurrentPeriodEnd,
            TrialEndsAt = sub.TrialEndsAt,
            CanceledAt = sub.CanceledAt
        };
    }
}
