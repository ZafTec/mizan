using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

// The admin side of the Pro catalogue - docs/ARCHITECTURE.md#billing. Every
// write goes to Paddle first and is stored only once Paddle has accepted it,
// so the catalogue never lists a price checkout would refuse.

internal static class ProProduct
{
    public const string Plan = "pro";
    public const string Name = "Mizan Pro";
    public const string Description = "Photo analysis, a working daily assistant allowance, the Telegram bot, and coach relationships.";
}

public record CreateBillingPlanCommand : IRequest<CreateBillingPlanResult>
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Interval { get; init; } = BillingIntervals.Month;
    public int AmountCents { get; init; }
    public int? TrialDays { get; init; }
    public int SortOrder { get; init; }
}

public record CreateBillingPlanResult(Guid Id);

public class CreateBillingPlanCommandValidator : AbstractValidator<CreateBillingPlanCommand>
{
    public CreateBillingPlanCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Interval).Must(BillingIntervals.IsValid).WithMessage("Interval must be month or year.");
        RuleFor(x => x.AmountCents).InclusiveBetween(100, 100_000).WithMessage("Price must be between $1.00 and $1,000.00.");
        RuleFor(x => x.TrialDays).InclusiveBetween(0, 90).When(x => x.TrialDays.HasValue);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 1000);
    }
}

public class CreateBillingPlanCommandHandler : IRequestHandler<CreateBillingPlanCommand, CreateBillingPlanResult>
{
    private readonly IMizanDbContext _context;
    private readonly IPaddleApiClient _paddle;
    private readonly HybridCache _cache;

    public CreateBillingPlanCommandHandler(IMizanDbContext context, IPaddleApiClient paddle, HybridCache cache)
    {
        _context = context;
        _paddle = paddle;
        _cache = cache;
    }

    public async Task<CreateBillingPlanResult> Handle(CreateBillingPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await BillingCatalog.CreatePlanAsync(
            _paddle,
            request.Name.Trim(),
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            request.Interval,
            request.AmountCents,
            request.TrialDays is > 0 ? request.TrialDays : null,
            request.SortOrder,
            cancellationToken);

        _context.BillingPlans.Add(plan);
        await _context.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByTagAsync(CacheTags.BillingPlans, cancellationToken);
        return new CreateBillingPlanResult(plan.Id);
    }
}

/// <summary>Name, description, order, and whether it is on sale. The price itself never changes in place.</summary>
public record UpdateBillingPlanCommand : IRequest<Unit>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int SortOrder { get; init; }
}

public class UpdateBillingPlanCommandValidator : AbstractValidator<UpdateBillingPlanCommand>
{
    public UpdateBillingPlanCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 1000);
    }
}

public class UpdateBillingPlanCommandHandler : IRequestHandler<UpdateBillingPlanCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly HybridCache _cache;

    public UpdateBillingPlanCommandHandler(IMizanDbContext context, HybridCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<Unit> Handle(UpdateBillingPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _context.BillingPlans.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("Billing plan", request.Id);

        plan.Name = request.Name.Trim();
        plan.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        plan.SortOrder = request.SortOrder;
        plan.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByTagAsync(CacheTags.BillingPlans, cancellationToken);
        return Unit.Value;
    }
}

/// <summary>
/// A new price for a plan: a new Paddle price and a new plan row, with the old
/// one archived. Existing subscribers keep renewing at the price they bought.
/// </summary>
public record ReplaceBillingPlanPriceCommand : IRequest<CreateBillingPlanResult>
{
    public Guid Id { get; init; }
    public int AmountCents { get; init; }
    public int? TrialDays { get; init; }
}

public class ReplaceBillingPlanPriceCommandValidator : AbstractValidator<ReplaceBillingPlanPriceCommand>
{
    public ReplaceBillingPlanPriceCommandValidator()
    {
        RuleFor(x => x.AmountCents).InclusiveBetween(100, 100_000).WithMessage("Price must be between $1.00 and $1,000.00.");
        RuleFor(x => x.TrialDays).InclusiveBetween(0, 90).When(x => x.TrialDays.HasValue);
    }
}

public class ReplaceBillingPlanPriceCommandHandler : IRequestHandler<ReplaceBillingPlanPriceCommand, CreateBillingPlanResult>
{
    private readonly IMizanDbContext _context;
    private readonly IPaddleApiClient _paddle;
    private readonly HybridCache _cache;

    public ReplaceBillingPlanPriceCommandHandler(IMizanDbContext context, IPaddleApiClient paddle, HybridCache cache)
    {
        _context = context;
        _paddle = paddle;
        _cache = cache;
    }

    public async Task<CreateBillingPlanResult> Handle(ReplaceBillingPlanPriceCommand request, CancellationToken cancellationToken)
    {
        var current = await _context.BillingPlans.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("Billing plan", request.Id);
        if (!current.IsActive)
        {
            throw new DomainValidationException("This plan is archived. Create a new plan instead.");
        }

        var replacement = await BillingCatalog.CreatePlanAsync(
            _paddle,
            current.Name,
            current.Description,
            current.Interval,
            request.AmountCents,
            request.TrialDays is > 0 ? request.TrialDays : null,
            current.SortOrder,
            cancellationToken);

        // Archive in Paddle before storing either row: if it fails, the admin
        // retries with both prices still on sale, never with neither.
        await _paddle.ArchivePriceAsync(current.PaddlePriceId, cancellationToken);
        var now = DateTime.UtcNow;
        current.IsActive = false;
        current.ArchivedAt = now;
        current.UpdatedAt = now;
        _context.BillingPlans.Add(replacement);

        // Discounts restricted to the old plan follow it to the new price.
        var discounts = await _context.BillingDiscounts
            .Where(d => d.IsActive && d.PlanIds.Contains(current.Id))
            .ToListAsync(cancellationToken);
        foreach (var discount in discounts)
        {
            discount.PlanIds = discount.PlanIds.Append(replacement.Id).ToList();
            discount.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByTagAsync(CacheTags.BillingPlans, cancellationToken);
        return new CreateBillingPlanResult(replacement.Id);
    }
}

public record ArchiveBillingPlanCommand(Guid Id) : IRequest<Unit>;

public class ArchiveBillingPlanCommandHandler : IRequestHandler<ArchiveBillingPlanCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly IPaddleApiClient _paddle;
    private readonly HybridCache _cache;

    public ArchiveBillingPlanCommandHandler(IMizanDbContext context, IPaddleApiClient paddle, HybridCache cache)
    {
        _context = context;
        _paddle = paddle;
        _cache = cache;
    }

    public async Task<Unit> Handle(ArchiveBillingPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _context.BillingPlans.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("Billing plan", request.Id);
        if (!plan.IsActive)
        {
            return Unit.Value;
        }

        // Archiving stops new checkouts at this price. Paddle keeps renewing
        // the subscriptions already on it.
        await _paddle.ArchivePriceAsync(plan.PaddlePriceId, cancellationToken);
        var now = DateTime.UtcNow;
        plan.IsActive = false;
        plan.ArchivedAt = now;
        plan.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByTagAsync(CacheTags.BillingPlans, cancellationToken);
        return Unit.Value;
    }
}

/// <summary>
/// Adopts the Pro prices already in Paddle that Mizan does not list yet: the
/// first run against an account whose prices were made in the dashboard.
/// </summary>
public record ImportBillingPlansCommand : IRequest<ImportBillingPlansResult>;

public record ImportBillingPlansResult(int Imported);

public class ImportBillingPlansCommandHandler : IRequestHandler<ImportBillingPlansCommand, ImportBillingPlansResult>
{
    private readonly IMizanDbContext _context;
    private readonly IPaddleApiClient _paddle;
    private readonly HybridCache _cache;

    public ImportBillingPlansCommandHandler(IMizanDbContext context, IPaddleApiClient paddle, HybridCache cache)
    {
        _context = context;
        _paddle = paddle;
        _cache = cache;
    }

    public async Task<ImportBillingPlansResult> Handle(ImportBillingPlansCommand request, CancellationToken cancellationToken)
    {
        var productId = await _paddle.EnsureProductAsync(ProProduct.Plan, ProProduct.Name, ProProduct.Description, cancellationToken);
        var prices = await _paddle.ListPricesAsync(productId, cancellationToken);
        var known = await _context.BillingPlans.Select(p => p.PaddlePriceId).ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var imported = 0;
        foreach (var price in prices.Where(p => BillingIntervals.IsValid(p.Interval) && !known.Contains(p.Id)))
        {
            _context.BillingPlans.Add(new BillingPlan
            {
                Id = Guid.NewGuid(),
                Name = price.Name,
                Description = null,
                Interval = price.Interval!,
                AmountCents = price.AmountCents,
                Currency = price.Currency,
                TrialDays = price.TrialDays,
                PaddleProductId = price.ProductId,
                PaddlePriceId = price.Id,
                IsActive = true,
                SortOrder = price.Interval == BillingIntervals.Month ? 0 : 10,
                CreatedAt = now,
                UpdatedAt = now
            });
            imported++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByTagAsync(CacheTags.BillingPlans, cancellationToken);
        return new ImportBillingPlansResult(imported);
    }
}

internal static class BillingCatalog
{
    public static async Task<BillingPlan> CreatePlanAsync(
        IPaddleApiClient paddle,
        string name,
        string? description,
        string interval,
        int amountCents,
        int? trialDays,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        var productId = await paddle.EnsureProductAsync(ProProduct.Plan, ProProduct.Name, ProProduct.Description, cancellationToken);
        var price = await paddle.CreatePriceAsync(
            new PaddlePriceRequest(productId, name, description, interval, amountCents, "USD", trialDays),
            cancellationToken);

        var now = DateTime.UtcNow;
        return new BillingPlan
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Interval = interval,
            AmountCents = price.AmountCents,
            Currency = price.Currency,
            TrialDays = price.TrialDays,
            PaddleProductId = productId,
            PaddlePriceId = price.Id,
            IsActive = true,
            SortOrder = sortOrder,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
