using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Prices;
using Mizan.Domain.Entities;
using Mizan.Domain.Pricing;

namespace Mizan.Application.Prices;

/// <summary>
/// An administrator types a price in: Estimated by default, or Observed when
/// they saw it on a shelf. It is filed under the region's Manual source.
/// </summary>
public record CreateManualPriceCommand(
    Guid FoodId,
    string RegionCode,
    decimal Price,
    decimal Quantity,
    string Unit,
    string? Confidence,
    DateTime? ObservedAt) : IRequest<Guid>, IRedactedAudit
{
    object IRedactedAudit.AuditDetails => new { FoodId, RegionCode, Price, Quantity, Unit, Confidence, ObservedAt };
}

public class CreateManualPriceCommandValidator : AbstractValidator<CreateManualPriceCommand>
{
    public CreateManualPriceCommandValidator()
    {
        RuleFor(x => x.FoodId).NotEmpty();
        RuleFor(x => x.RegionCode).NotEmpty();
        RuleFor(x => x.Price).GreaterThan(0).LessThan(10_000_000);
        RuleFor(x => x.Quantity).GreaterThan(0).LessThan(10_000);
        RuleFor(x => x.Unit).Must(PriceUnits.IsValid).WithMessage("The unit is g, kg, ml, l or piece.");
        RuleFor(x => x.Confidence)
            .Must(c => c is null || string.Equals(c, nameof(PriceConfidence.Estimated), StringComparison.OrdinalIgnoreCase)
                || string.Equals(c, nameof(PriceConfidence.Observed), StringComparison.OrdinalIgnoreCase))
            .WithMessage("A manual price is Estimated or Observed; Reported prices come from accepted reports.");
    }
}

public class CreateManualPriceCommandHandler : IRequestHandler<CreateManualPriceCommand, Guid>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly PriceBookReader _prices;
    private readonly HybridCache _cache;

    public CreateManualPriceCommandHandler(IMizanDbContext context, ICurrentUserService currentUser, PriceBookReader prices, HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _prices = prices;
        _cache = cache;
    }

    public async Task<Guid> Handle(CreateManualPriceCommand request, CancellationToken cancellationToken)
    {
        PriceAdmin.Require(_currentUser);
        var region = await _prices.RequireRegionAsync(request.RegionCode, cancellationToken);
        var food = await _context.Foods.AsNoTracking().FirstOrDefaultAsync(f => f.Id == request.FoodId, cancellationToken)
            ?? throw new EntityNotFoundException("Food", request.FoodId);

        var confidence = request.Confidence is null
            ? PriceConfidence.Estimated
            : Enum.Parse<PriceConfidence>(request.Confidence, ignoreCase: true);
        var source = await _prices.HouseSourceAsync(region.Code, PriceSourceKind.Manual, cancellationToken);
        var now = DateTime.UtcNow;
        var price = new FoodPrice
        {
            Id = Guid.NewGuid(),
            FoodId = food.Id,
            RegionCode = region.Code,
            SourceId = source.Id,
            Price = request.Price,
            Quantity = request.Quantity,
            Unit = PriceUnits.Normalize(request.Unit),
            Confidence = confidence,
            ObservedAt = request.ObservedAt is { } at ? DateTime.SpecifyKind(at, DateTimeKind.Utc) : now,
            CreatedAt = now,
        };
        if (PriceBook.PerGram(price, food) is null)
            throw new DomainValidationException("That quantity and unit do not convert to grams for this food.");

        _context.FoodPrices.Add(price);
        await _context.SaveChangesAsync(cancellationToken);
        await PriceBookReader.InvalidateAsync(_cache, cancellationToken);
        return price.Id;
    }
}

/// <summary>
/// Writes <see cref="AddisAbabaStaplePrices"/> as Estimated prices for the
/// catalogue foods whose names match. Safe to run again: a food that already
/// has an Estimated price in the region is left alone.
/// </summary>
public record SeedStaplePricesCommand : IRequest<SeedStaplePricesResult>;

public class SeedStaplePricesCommandHandler : IRequestHandler<SeedStaplePricesCommand, SeedStaplePricesResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly PriceBookReader _prices;
    private readonly HybridCache _cache;
    private readonly ILogger<SeedStaplePricesCommandHandler> _logger;

    public SeedStaplePricesCommandHandler(
        IMizanDbContext context, ICurrentUserService currentUser, PriceBookReader prices, HybridCache cache, ILogger<SeedStaplePricesCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _prices = prices;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SeedStaplePricesResult> Handle(SeedStaplePricesCommand request, CancellationToken cancellationToken)
    {
        PriceAdmin.Require(_currentUser);
        var region = await _prices.RequireRegionAsync(AddisAbabaStaplePrices.RegionCode, cancellationToken);

        var names = AddisAbabaStaplePrices.All.SelectMany(s => s.Names).Select(n => n.ToLowerInvariant()).ToList();
        var foods = await _context.Foods.AsNoTracking()
            .Where(f => f.UserId == null && names.Contains(f.Name.ToLower()))
            .OrderByDescending(f => f.IsVerified).ThenBy(f => f.CreatedAt)
            .ToListAsync(cancellationToken);
        var foodIds = foods.Select(f => f.Id).ToList();
        var estimated = (await _context.FoodPrices.AsNoTracking()
                .Where(p => p.RegionCode == region.Code && p.Confidence == PriceConfidence.Estimated && foodIds.Contains(p.FoodId))
                .Select(p => p.FoodId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var source = await _prices.HouseSourceAsync(region.Code, PriceSourceKind.Manual, cancellationToken);
        var now = DateTime.UtcNow;
        int stored = 0, alreadyPriced = 0;
        var missing = new List<string>();
        foreach (var staple in AddisAbabaStaplePrices.All)
        {
            // The first name that matches wins; a catalogue with "Onion" and "Red onion" prices the first listed.
            var food = staple.Names
                .Select(n => foods.FirstOrDefault(f => string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(f => f is not null);
            if (food is null)
            {
                missing.Add(staple.Names[0]);
                continue;
            }
            if (!estimated.Add(food.Id))
            {
                alreadyPriced++;
                continue;
            }

            _context.FoodPrices.Add(new FoodPrice
            {
                Id = Guid.NewGuid(),
                FoodId = food.Id,
                RegionCode = region.Code,
                SourceId = source.Id,
                Price = staple.Price,
                Quantity = staple.Quantity,
                Unit = staple.Unit,
                Confidence = PriceConfidence.Estimated,
                ObservedAt = AddisAbabaStaplePrices.ObservedAt,
                CreatedAt = now,
            });
            stored++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        if (stored > 0) await PriceBookReader.InvalidateAsync(_cache, cancellationToken);
        _logger.LogInformation(
            "Staple prices for {Region}: {Stored} stored, {AlreadyPriced} already priced, {Missing} with no catalogue food",
            region.Code, stored, alreadyPriced, missing.Count);
        return new SeedStaplePricesResult(stored, alreadyPriced, missing);
    }
}
