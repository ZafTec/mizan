using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Prices;
using Mizan.Domain.Entities;

namespace Mizan.Application.Prices;

/// <summary>
/// POST /api/admin/prices/import-starter-foods: adds the <see cref="StarterFoods"/> to the shared
/// catalogue. Safe to repeat: a food whose derived id exists, or a shared food with the same name,
/// is left alone, so an administrator's corrections stay.
/// </summary>
public record ImportStarterFoodsCommand : IRequest<ImportStarterFoodsResult>;

public class ImportStarterFoodsCommandHandler : IRequestHandler<ImportStarterFoodsCommand, ImportStarterFoodsResult>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly HybridCache _cache;
    private readonly ILogger<ImportStarterFoodsCommandHandler> _logger;

    public ImportStarterFoodsCommandHandler(
        IMizanDbContext context, ICurrentUserService currentUser, HybridCache cache, ILogger<ImportStarterFoodsCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ImportStarterFoodsResult> Handle(ImportStarterFoodsCommand request, CancellationToken cancellationToken)
    {
        PriceAdmin.Require(_currentUser);

        var ids = StarterFoods.All.Select(f => StarterFoods.IdFor(f.Name)).ToList();
        var names = StarterFoods.All.Select(f => f.Name.ToLower()).ToList();
        var existing = await _context.Foods.AsNoTracking()
            .Where(f => ids.Contains(f.Id) || (f.UserId == null && names.Contains(f.Name.ToLower())))
            .Select(f => new { f.Id, f.Name })
            .ToListAsync(cancellationToken);
        var existingIds = existing.Select(f => f.Id).ToHashSet();
        var existingNames = existing.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var created = new List<string>();
        foreach (var starter in StarterFoods.All)
        {
            var id = StarterFoods.IdFor(starter.Name);
            if (existingIds.Contains(id) || existingNames.Contains(starter.Name)) continue;
            _context.Foods.Add(new Food
            {
                Id = id,
                UserId = null,
                Name = starter.Name,
                ServingSize = 100,
                ServingUnit = "g",
                CaloriesPer100g = starter.Kcal,
                ProteinPer100g = starter.Protein,
                CarbsPer100g = starter.Carbs,
                FatPer100g = starter.Fat,
                FiberPer100g = starter.Fiber,
                ProteinCalorieRatio = Food.ComputeProteinCalorieRatio(starter.Kcal, starter.Protein),
                IsVerified = starter.IsSourced,
                CreatedAt = now,
                UpdatedAt = now,
            });
            created.Add(starter.Name);
        }

        if (created.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _cache.RemoveByTagAsync(CacheTags.Foods, cancellationToken);
        }
        _logger.LogInformation("Starter foods: {Created} created, {Existing} already in the catalogue",
            created.Count, StarterFoods.All.Count - created.Count);
        return new ImportStarterFoodsResult(created.Count, StarterFoods.All.Count - created.Count, created);
    }
}
