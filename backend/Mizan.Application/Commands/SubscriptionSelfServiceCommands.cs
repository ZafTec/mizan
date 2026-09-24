using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Billing;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Application.Queries;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

// What a subscriber does from the billing page - docs/ARCHITECTURE.md#billing.
// Each command asks Paddle to make the change, stores the subscription Paddle
// returns, and answers with the account as it now stands. The webhook that
// follows carries the same state and changes nothing.

public record PlanChangePreviewDto
{
    public Guid PlanId { get; init; }
    public string PlanName { get; init; } = string.Empty;

    /// <summary>Charged now; negative when the switch leaves a credit toward later bills.</summary>
    public int DueNowCents { get; init; }
    public string Currency { get; init; } = "USD";
    public DateTime? NextBilledAt { get; init; }
    public int? NextAmountCents { get; init; }
}

public record PreviewPlanChangeQuery(Guid PlanId) : IRequest<PlanChangePreviewDto>;

public class PreviewPlanChangeQueryHandler : IRequestHandler<PreviewPlanChangeQuery, PlanChangePreviewDto>
{
    private readonly SubscriberBilling _billing;
    private readonly IPaddleApiClient _paddle;

    public PreviewPlanChangeQueryHandler(SubscriberBilling billing, IPaddleApiClient paddle)
    {
        _billing = billing;
        _paddle = paddle;
    }

    public async Task<PlanChangePreviewDto> Handle(PreviewPlanChangeQuery request, CancellationToken cancellationToken)
    {
        var (sub, plan) = await _billing.LoadForChangeAsync(request.PlanId, cancellationToken);
        var preview = await _paddle.PreviewPriceChangeAsync(sub.PaddleSubscriptionId!, plan.PaddlePriceId, cancellationToken);
        return new PlanChangePreviewDto
        {
            PlanId = plan.Id,
            PlanName = plan.Name,
            DueNowCents = preview.DueNowCents,
            Currency = preview.Currency,
            NextBilledAt = preview.NextBilledAt,
            NextAmountCents = preview.NextAmountCents
        };
    }
}

public record ChangeSubscriptionPlanCommand(Guid PlanId) : IRequest<MySubscriptionDto>;

public class ChangeSubscriptionPlanCommandHandler : IRequestHandler<ChangeSubscriptionPlanCommand, MySubscriptionDto>
{
    private readonly SubscriberBilling _billing;
    private readonly IPaddleApiClient _paddle;

    public ChangeSubscriptionPlanCommandHandler(SubscriberBilling billing, IPaddleApiClient paddle)
    {
        _billing = billing;
        _paddle = paddle;
    }

    public async Task<MySubscriptionDto> Handle(ChangeSubscriptionPlanCommand request, CancellationToken cancellationToken)
    {
        var (sub, plan) = await _billing.LoadForChangeAsync(request.PlanId, cancellationToken);
        var state = await _paddle.ChangePriceAsync(sub.PaddleSubscriptionId!, plan.PaddlePriceId, cancellationToken);
        return await _billing.StoreAsync(sub, state, cancellationToken);
    }
}

public record CancelSubscriptionCommand : IRequest<MySubscriptionDto>;

public class CancelSubscriptionCommandHandler : IRequestHandler<CancelSubscriptionCommand, MySubscriptionDto>
{
    private readonly SubscriberBilling _billing;
    private readonly IPaddleApiClient _paddle;

    public CancelSubscriptionCommandHandler(SubscriberBilling billing, IPaddleApiClient paddle)
    {
        _billing = billing;
        _paddle = paddle;
    }

    public async Task<MySubscriptionDto> Handle(CancelSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var sub = await _billing.LoadManageableAsync(cancellationToken);
        if (sub.ScheduledChangeAction == "cancel")
        {
            return await _billing.CurrentAsync(sub, cancellationToken);
        }

        var state = await _paddle.CancelAtPeriodEndAsync(sub.PaddleSubscriptionId!, cancellationToken);
        return await _billing.StoreAsync(sub, state, cancellationToken);
    }
}

public record ResumeSubscriptionCommand : IRequest<MySubscriptionDto>;

public class ResumeSubscriptionCommandHandler : IRequestHandler<ResumeSubscriptionCommand, MySubscriptionDto>
{
    private readonly SubscriberBilling _billing;
    private readonly IPaddleApiClient _paddle;

    public ResumeSubscriptionCommandHandler(SubscriberBilling billing, IPaddleApiClient paddle)
    {
        _billing = billing;
        _paddle = paddle;
    }

    public async Task<MySubscriptionDto> Handle(ResumeSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var sub = await _billing.LoadManageableAsync(cancellationToken);
        if (sub.ScheduledChangeAction is null)
        {
            return await _billing.CurrentAsync(sub, cancellationToken);
        }

        var state = await _paddle.RemoveScheduledChangeAsync(sub.PaddleSubscriptionId!, cancellationToken);
        return await _billing.StoreAsync(sub, state, cancellationToken);
    }
}

public record BillingTransactionDto
{
    public string Id { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? BilledAt { get; init; }
    public int TotalCents { get; init; }
    public string Currency { get; init; } = "USD";
    public string? InvoiceNumber { get; init; }
}

public record GetBillingHistoryQuery : IRequest<IReadOnlyList<BillingTransactionDto>>;

public class GetBillingHistoryQueryHandler : IRequestHandler<GetBillingHistoryQuery, IReadOnlyList<BillingTransactionDto>>
{
    private readonly SubscriberBilling _billing;
    private readonly IPaddleApiClient _paddle;

    public GetBillingHistoryQueryHandler(SubscriberBilling billing, IPaddleApiClient paddle)
    {
        _billing = billing;
        _paddle = paddle;
    }

    public async Task<IReadOnlyList<BillingTransactionDto>> Handle(GetBillingHistoryQuery request, CancellationToken cancellationToken)
    {
        var customerId = await _billing.CustomerIdAsync(cancellationToken);
        if (customerId is null)
        {
            return Array.Empty<BillingTransactionDto>();
        }

        var transactions = await _paddle.ListTransactionsAsync(customerId, cancellationToken);
        return transactions.Select(t => new BillingTransactionDto
        {
            Id = t.Id,
            Status = t.Status,
            BilledAt = t.BilledAt,
            TotalCents = t.TotalCents,
            Currency = t.Currency,
            InvoiceNumber = t.InvoiceNumber
        }).ToList();
    }
}

public record InvoiceLinkDto(string Url);

public record GetInvoiceLinkQuery(string TransactionId) : IRequest<InvoiceLinkDto>;

public class GetInvoiceLinkQueryHandler : IRequestHandler<GetInvoiceLinkQuery, InvoiceLinkDto>
{
    private readonly SubscriberBilling _billing;
    private readonly IPaddleApiClient _paddle;

    public GetInvoiceLinkQueryHandler(SubscriberBilling billing, IPaddleApiClient paddle)
    {
        _billing = billing;
        _paddle = paddle;
    }

    public async Task<InvoiceLinkDto> Handle(GetInvoiceLinkQuery request, CancellationToken cancellationToken)
    {
        var customerId = await _billing.CustomerIdAsync(cancellationToken)
            ?? throw new EntityNotFoundException("Invoice", request.TransactionId);

        // The transaction id comes from the browser; Paddle's answer, not the
        // id, decides whether it is this customer's.
        var url = await _paddle.GetInvoiceUrlAsync(request.TransactionId, customerId, cancellationToken);
        return url is null
            ? throw new EntityNotFoundException("Invoice", request.TransactionId)
            : new InvoiceLinkDto(url);
    }
}

/// <summary>The signed-in subscriber's billing row, loaded and stored the one way every self-service command needs.</summary>
public class SubscriberBilling
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IEntitlementService _entitlements;

    public SubscriberBilling(IMizanDbContext context, ICurrentUserService currentUser, IEntitlementService entitlements)
    {
        _context = context;
        _currentUser = currentUser;
        _entitlements = entitlements;
    }

    private Guid UserId => _currentUser.UserId ?? throw new UnauthorizedAccessException("User must be authenticated");

    public async Task<string?> CustomerIdAsync(CancellationToken cancellationToken)
    {
        var userId = UserId;
        return await _context.Subscriptions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.PaddleCustomerId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Subscription> LoadManageableAsync(CancellationToken cancellationToken)
    {
        var userId = UserId;
        var sub = await _context.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        if (sub is null || sub.IsLifetime || string.IsNullOrEmpty(sub.PaddleSubscriptionId)
            || sub.Status is not ("active" or "trialing" or "past_due"))
        {
            throw new DomainValidationException("There is no active subscription to change.");
        }

        return sub;
    }

    public async Task<(Subscription Sub, BillingPlan Plan)> LoadForChangeAsync(Guid planId, CancellationToken cancellationToken)
    {
        var sub = await LoadManageableAsync(cancellationToken);
        if (sub.ScheduledChangeAction == "cancel")
        {
            throw new DomainValidationException("Your subscription is set to end. Keep it first, then switch plans.");
        }

        var plan = await _context.BillingPlans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, cancellationToken)
            ?? throw new EntityNotFoundException("Billing plan", planId);
        if (plan.PaddlePriceId == sub.PaddlePriceId)
        {
            throw new DomainValidationException("You are already on this plan.");
        }

        return (sub, plan);
    }

    public async Task<MySubscriptionDto> StoreAsync(Subscription sub, PaddleSubscriptionState state, CancellationToken cancellationToken)
    {
        if (!state.IsOlderThan(sub))
        {
            state.ApplyTo(sub, DateTime.UtcNow);
            await _context.SaveChangesAsync(cancellationToken);
        }

        await _entitlements.InvalidateAsync(sub.UserId, cancellationToken);
        return await CurrentAsync(sub, cancellationToken);
    }

    public async Task<MySubscriptionDto> CurrentAsync(Subscription sub, CancellationToken cancellationToken)
    {
        var entitlement = await _entitlements.GetAsync(sub.UserId, cancellationToken);
        var plan = sub.PaddlePriceId is { } priceId
            ? await _context.BillingPlans.AsNoTracking().FirstOrDefaultAsync(p => p.PaddlePriceId == priceId, cancellationToken)
            : null;
        return MySubscriptionDto.From(sub, plan, entitlement.IsPro);
    }
}
