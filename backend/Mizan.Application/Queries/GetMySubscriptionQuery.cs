using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

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
    public DateTime? NextBilledAt { get; init; }

    /// <summary>Set while a cancellation is scheduled: Pro continues until then.</summary>
    public DateTime? CancelsAt { get; init; }

    /// <summary>The catalogue plan the subscription is billed at, when Mizan lists its price.</summary>
    public Guid? PlanId { get; init; }
    public string? PlanName { get; init; }
    public string? Interval { get; init; }
    public int? AmountCents { get; init; }
    public string? Currency { get; init; }

    /// <summary>A Paddle subscription exists that this account can change, cancel, or resume from Mizan.</summary>
    public bool CanManage { get; init; }

    /// <summary>A Paddle customer exists, so invoices and the hosted portal are available.</summary>
    public bool HasBillingAccount { get; init; }

    public static MySubscriptionDto From(Subscription? sub, BillingPlan? plan, bool isPro)
    {
        if (sub is null)
        {
            return new MySubscriptionDto { IsPro = isPro };
        }

        var manageable = !sub.IsLifetime
            && !string.IsNullOrEmpty(sub.PaddleSubscriptionId)
            && sub.Status is "active" or "trialing" or "past_due";

        return new MySubscriptionDto
        {
            Plan = sub.Plan,
            Status = sub.Status,
            IsPro = isPro,
            IsLifetime = sub.IsLifetime,
            CurrentPeriodEnd = sub.CurrentPeriodEnd,
            TrialEndsAt = sub.TrialEndsAt,
            CanceledAt = sub.CanceledAt,
            NextBilledAt = sub.NextBilledAt,
            CancelsAt = sub.ScheduledChangeAction == "cancel" ? sub.ScheduledChangeAt : null,
            PlanId = plan?.Id,
            PlanName = plan?.Name,
            Interval = plan?.Interval,
            AmountCents = plan?.AmountCents,
            Currency = plan?.Currency,
            CanManage = manageable,
            HasBillingAccount = !string.IsNullOrEmpty(sub.PaddleCustomerId)
        };
    }
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

        var sub = await _context.Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        var plan = sub?.PaddlePriceId is { } priceId
            ? await _context.BillingPlans.AsNoTracking().FirstOrDefaultAsync(p => p.PaddlePriceId == priceId, cancellationToken)
            : null;

        return MySubscriptionDto.From(sub, plan, entitlement.IsPro);
    }
}
