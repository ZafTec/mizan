using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

/// <summary>
/// A Paddle discount. Without a code it is a deal checkout applies by itself
/// and the pricing page advertises; with one, only a customer who enters the
/// code gets it.
/// </summary>
public record CreateBillingDiscountCommand : IRequest<CreateBillingDiscountResult>
{
    public string Label { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string Type { get; init; } = BillingDiscountTypes.Percentage;

    /// <summary>A percentage, or cents off for a flat discount.</summary>
    public decimal Amount { get; init; }
    public bool Recurring { get; init; }
    public int? MaximumRecurringIntervals { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public List<Guid> PlanIds { get; init; } = new();
}

public record CreateBillingDiscountResult(Guid Id);

public class CreateBillingDiscountCommandValidator : AbstractValidator<CreateBillingDiscountCommand>
{
    public CreateBillingDiscountCommandValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Code)
            .Matches("^[A-Za-z0-9]{3,16}$")
            .When(x => !string.IsNullOrEmpty(x.Code))
            .WithMessage("A code is 3 to 16 letters or digits.");
        RuleFor(x => x.Type).Must(BillingDiscountTypes.IsValid).WithMessage("Type must be percentage or flat.");
        RuleFor(x => x.Amount)
            .InclusiveBetween(1, 100)
            .When(x => x.Type == BillingDiscountTypes.Percentage)
            .WithMessage("A percentage discount is between 1 and 100.");
        RuleFor(x => x.Amount)
            .InclusiveBetween(1, 100_000)
            .Must(a => a == decimal.Truncate(a))
            .When(x => x.Type == BillingDiscountTypes.Flat)
            .WithMessage("A flat discount is a whole number of cents.");
        RuleFor(x => x.MaximumRecurringIntervals)
            .InclusiveBetween(1, 120)
            .When(x => x.Recurring && x.MaximumRecurringIntervals.HasValue);
        RuleFor(x => x.ExpiresAt)
            .Must(e => e > DateTime.UtcNow)
            .When(x => x.ExpiresAt.HasValue)
            .WithMessage("The expiry must be in the future.");
    }
}

public class CreateBillingDiscountCommandHandler : IRequestHandler<CreateBillingDiscountCommand, CreateBillingDiscountResult>
{
    private readonly IMizanDbContext _context;
    private readonly IPaddleApiClient _paddle;
    private readonly HybridCache _cache;

    public CreateBillingDiscountCommandHandler(IMizanDbContext context, IPaddleApiClient paddle, HybridCache cache)
    {
        _context = context;
        _paddle = paddle;
        _cache = cache;
    }

    public async Task<CreateBillingDiscountResult> Handle(CreateBillingDiscountCommand request, CancellationToken cancellationToken)
    {
        var planIds = request.PlanIds.Distinct().ToList();
        var plans = await _context.BillingPlans
            .Where(p => planIds.Contains(p.Id) && p.IsActive)
            .ToListAsync(cancellationToken);
        if (plans.Count != planIds.Count)
        {
            throw new DomainValidationException("A discount can only apply to plans that are on sale.");
        }

        var code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim().ToUpperInvariant();
        if (code is not null && await _context.BillingDiscounts.AnyAsync(d => d.Code == code, cancellationToken))
        {
            throw new DomainValidationException($"The code {code} is already in use.");
        }

        var expiresAt = request.ExpiresAt?.ToUniversalTime();
        var paddleId = await _paddle.CreateDiscountAsync(
            new PaddleDiscountRequest(
                request.Label.Trim(),
                code,
                request.Type,
                request.Amount,
                "USD",
                request.Recurring,
                request.Recurring ? request.MaximumRecurringIntervals : null,
                expiresAt,
                plans.Select(p => p.PaddlePriceId).ToList()),
            cancellationToken);

        var now = DateTime.UtcNow;
        var discount = new BillingDiscount
        {
            Id = Guid.NewGuid(),
            Label = request.Label.Trim(),
            Code = code,
            Type = request.Type,
            Amount = request.Amount,
            Currency = "USD",
            Recurring = request.Recurring,
            MaximumRecurringIntervals = request.Recurring ? request.MaximumRecurringIntervals : null,
            ExpiresAt = expiresAt,
            PlanIds = planIds,
            PaddleDiscountId = paddleId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.BillingDiscounts.Add(discount);
        await _context.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByTagAsync(CacheTags.BillingPlans, cancellationToken);
        return new CreateBillingDiscountResult(discount.Id);
    }
}

public record ArchiveBillingDiscountCommand(Guid Id) : IRequest<Unit>;

public class ArchiveBillingDiscountCommandHandler : IRequestHandler<ArchiveBillingDiscountCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly IPaddleApiClient _paddle;
    private readonly HybridCache _cache;

    public ArchiveBillingDiscountCommandHandler(IMizanDbContext context, IPaddleApiClient paddle, HybridCache cache)
    {
        _context = context;
        _paddle = paddle;
        _cache = cache;
    }

    public async Task<Unit> Handle(ArchiveBillingDiscountCommand request, CancellationToken cancellationToken)
    {
        var discount = await _context.BillingDiscounts.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("Billing discount", request.Id);
        if (!discount.IsActive)
        {
            return Unit.Value;
        }

        // Stops new checkouts using it. Subscriptions it already applies to
        // keep it for the periods they were promised.
        await _paddle.ArchiveDiscountAsync(discount.PaddleDiscountId, cancellationToken);
        discount.IsActive = false;
        discount.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByTagAsync(CacheTags.BillingPlans, cancellationToken);
        return Unit.Value;
    }
}
