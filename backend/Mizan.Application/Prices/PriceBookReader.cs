using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Domain.Pricing;

namespace Mizan.Application.Prices;

/// <summary>The region whose prices a person sees, with its currency.</summary>
public sealed record PriceRegionInfo(string Code, string Currency);

/// <summary>
/// Loads what <see cref="PriceBook"/> needs: the viewer's region, and the
/// candidate price rows for a set of foods in it. Every read that shows a price
/// or a cost goes through here, so the region rule lives once.
/// </summary>
public sealed class PriceBookReader
{
    /// <summary>Newest rows kept per food and confidence. PriceBook needs the newest usable one; the second covers a row whose unit does not convert.</summary>
    private const int RowsPerConfidence = 2;

    private readonly IMizanDbContext _context;

    public PriceBookReader(IMizanDbContext context) => _context = context;

    /// <summary>
    /// The user's chosen region when it exists and is enabled, else the default
    /// region. A region an administrator disabled falls back rather than
    /// leaving the person with no prices.
    /// </summary>
    public async Task<PriceRegionInfo> RegionForAsync(Guid userId, CancellationToken cancellationToken)
    {
        var chosen = await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.PriceRegionCode)
            .FirstOrDefaultAsync(cancellationToken);

        var regions = await _context.PriceRegions.AsNoTracking()
            .Where(r => r.Code == (chosen ?? PriceRegionDefaults.Code) || r.Code == PriceRegionDefaults.Code)
            .ToListAsync(cancellationToken);
        var region = regions.FirstOrDefault(r => r.Code == chosen && r.Enabled)
            ?? regions.FirstOrDefault(r => r.Code == PriceRegionDefaults.Code);
        return region is null
            ? new PriceRegionInfo(PriceRegionDefaults.Code, string.Empty)
            : new PriceRegionInfo(region.Code, region.Currency);
    }

    /// <summary>Quotes for the foods in the region, keyed by food id. Foods without a usable price are absent.</summary>
    public async Task<IReadOnlyDictionary<Guid, FoodPriceQuote>> QuotesAsync(
        IReadOnlyCollection<Food> foods, PriceRegionInfo region, CancellationToken cancellationToken)
    {
        if (foods.Count == 0) return new Dictionary<Guid, FoodPriceQuote>();
        var foodIds = foods.Select(f => f.Id).Distinct().ToList();

        // The newest few rows per food and confidence, as a lateral join, so a
        // food observed daily for years does not load its whole history.
        var prices = _context.FoodPrices.AsNoTracking();
        var rows = await prices
            .Where(p => foodIds.Contains(p.FoodId) && p.RegionCode == region.Code)
            .Select(p => new { p.FoodId, p.Confidence })
            .Distinct()
            .SelectMany(key => prices
                .Where(p => p.FoodId == key.FoodId && p.RegionCode == region.Code && p.Confidence == key.Confidence)
                .OrderByDescending(p => p.ObservedAt).ThenByDescending(p => p.CreatedAt)
                .Take(RowsPerConfidence))
            .ToListAsync(cancellationToken);

        var sourceIds = rows.Where(r => r.SourceId.HasValue).Select(r => r.SourceId!.Value).Distinct().ToList();
        var sourceNames = sourceIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _context.PriceSources.AsNoTracking()
                .Where(s => sourceIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        return PriceBook.QuoteAll(foods, region.Code, region.Currency, rows, sourceNames);
    }

    /// <summary>The source an administrator's typed-in prices, or accepted reports, are filed under; created the first time a region needs it.</summary>
    public async Task<PriceSource> HouseSourceAsync(string regionCode, PriceSourceKind kind, CancellationToken cancellationToken)
    {
        var existing = await _context.PriceSources
            .Where(s => s.RegionCode == regionCode && s.Kind == kind)
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null) return existing;

        var name = kind == PriceSourceKind.Report ? "User reports" : "Manual";
        if (await _context.PriceSources.AnyAsync(s => s.RegionCode == regionCode && s.Name == name, cancellationToken))
            name = $"{name} ({kind})";

        var now = DateTime.UtcNow;
        var source = new PriceSource
        {
            Id = Guid.NewGuid(),
            Name = name,
            Kind = kind,
            RegionCode = regionCode,
            Enabled = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _context.PriceSources.Add(source);
        return source;
    }

    /// <summary>A region that exists, or a validation error naming the code.</summary>
    public async Task<PriceRegion> RequireRegionAsync(string code, CancellationToken cancellationToken) =>
        await _context.PriceRegions.FirstOrDefaultAsync(r => r.Code == code, cancellationToken)
        ?? throw new DomainValidationException($"There is no price region '{code}'.");

    /// <summary>Clears every cached read that shows a price or a cost.</summary>
    public static async Task InvalidateAsync(HybridCache cache, CancellationToken cancellationToken) =>
        await cache.RemoveByTagAsync(CacheTags.Prices, cancellationToken);
}
