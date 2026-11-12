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
    private readonly ICurrentUserService _currentUser;
    private readonly StarterFoodImport _import;

    public ImportStarterFoodsCommandHandler(ICurrentUserService currentUser, StarterFoodImport import)
    {
        _currentUser = currentUser;
        _import = import;
    }

    public Task<ImportStarterFoodsResult> Handle(ImportStarterFoodsCommand request, CancellationToken cancellationToken)
    {
        PriceAdmin.Require(_currentUser);
        return _import.ImportAsync(cancellationToken);
    }
}

/// <summary>
/// Puts the <see cref="StarterFoods"/> in the shared catalogue. An administrator runs it from the
/// prices page; the migration step runs it when the catalogue has no shared food, so a fresh install
/// can log and plan from the start (docs/PLANNING.md#api-step-3-as-built).
/// </summary>
public class StarterFoodImport
{
    private readonly IMizanDbContext _context;
    private readonly HybridCache _cache;
    private readonly ILogger<StarterFoodImport> _logger;

    public StarterFoodImport(IMizanDbContext context, HybridCache cache, ILogger<StarterFoodImport> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>Imports the starter foods only when there is no shared food yet. Returns null when it did nothing.</summary>
    public async Task<ImportStarterFoodsResult?> ImportIfEmptyAsync(CancellationToken cancellationToken)
    {
        if (await _context.Foods.AnyAsync(f => f.UserId == null, cancellationToken)) return null;
        return await ImportAsync(cancellationToken);
    }

    public async Task<ImportStarterFoodsResult> ImportAsync(CancellationToken cancellationToken)
    {
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
